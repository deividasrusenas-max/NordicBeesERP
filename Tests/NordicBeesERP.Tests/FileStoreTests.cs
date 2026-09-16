using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Storage;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for FileStore (unified file storage, content-addressed blob
/// store backed by the `files` table). Runs against the real nordic_bees_erp_test
/// database (same global QueryTrackingBehavior.NoTracking as production) and a
/// per-class temp directory on disk. The `files` table is NOT EF-mapped — every
/// read/write here is parameterized raw SQL. All rows created carry a unique
/// class marker in created_by and are deleted in teardown; the temp root is
/// deleted in teardown as well.
/// </summary>
public class FileStoreTests : IClassFixture<DbTestFixture>, IDisposable
{
    private readonly DbTestFixture _fixture;
    private readonly string _root;
    private readonly string _marker;

    public FileStoreTests(DbTestFixture fixture)
    {
        _fixture = fixture;
        _root = Path.Combine(Path.GetTempPath(), "nordicbees-filestore-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _marker = "nbt-fstest-" + Guid.NewGuid().ToString("N");
    }

    public void Dispose()
    {
        try
        {
            // Delete every files row this class created (covers all 5 tests, including
            // the interrupted-write test's suffixed marker) — parameterized.
            using var db = _fixture.Factory.CreateDbContext();
            db.Database.ExecuteSqlRaw(
                "DELETE FROM files WHERE created_by LIKE {0}", _marker + "%");

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // Best-effort cleanup: a failed teardown must not mask the real test result.
        }
    }

    private FileStore NewStore() =>
        new(Options.Create(new FileStorageOptions { Root = _root }), _fixture.Factory);

    [Fact]
    public async Task SaveAsync_ThenOpenAsync_RoundTripsExactBytes()
    {
        var store = NewStore();
        var content = Encoding.UTF8.GetBytes($"filestore-roundtrip-{Guid.NewGuid():N}".PadRight(4096, '.'));

        var saved = await store.SaveAsync(new MemoryStream(content), new FileMetadata("expenses", "test", entityId: null, createdBy: _marker), CancellationToken.None);

        Assert.True(saved.Id > 0);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), saved.Sha256);
        Assert.Equal(content.Length, saved.ByteSize);

        await using (var stream = await store.OpenAsync(saved.Id, CancellationToken.None))
        {
            var roundTripped = new MemoryStream();
            await stream.CopyToAsync(roundTripped);
            Assert.Equal(content, roundTripped.ToArray());
        }

        // Re-verify the row from a BRAND NEW DbContext — do not trust in-memory state.
        await using (var verifyContext = await _fixture.Factory.CreateDbContextAsync())
        {
            var count = await verifyContext.Database
                .SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM files WHERE id = {0} AND sha256 = {1}", saved.Id, saved.Sha256)
                .SingleAsync();
            Assert.Equal(1L, count);
        }
    }

    [Fact]
    public async Task SaveAsync_SameContentTwice_ProducesOneBlobAndTwoRows()
    {
        var store = NewStore();
        var content = Encoding.UTF8.GetBytes($"filestore-dedup-{Guid.NewGuid():N}".PadRight(4096, '.'));

        var first = await store.SaveAsync(new MemoryStream(content), new FileMetadata("expenses", "test", entityId: null, createdBy: _marker), CancellationToken.None);

        var second = await store.SaveAsync(new MemoryStream(content), new FileMetadata("invoices", "test", entityId: null, createdBy: _marker), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);

        // Two distinct rows, identical sha256.
        await using (var verifyContext = await _fixture.Factory.CreateDbContextAsync())
        {
            var conn = verifyContext.Database.GetDbConnection();
            var openedByUs = conn.State != ConnectionState.Open;
            if (openedByUs) await conn.OpenAsync();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT sha256 FROM files WHERE created_by = @p0 ORDER BY id";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = _marker;
                cmd.Parameters.Add(p);

                var shas = new List<string>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    shas.Add(reader.GetString(0));
                }

                Assert.Equal(2, shas.Count);
                Assert.Equal(first.Sha256, second.Sha256);
                Assert.All(shas, s => Assert.Equal(first.Sha256, s));
            }
            finally
            {
                if (openedByUs) await conn.CloseAsync();
            }
        }

        // EXACTLY ONE blob file anywhere under blobs/.
        var blobsDir = Path.Combine(_root, "blobs");
        var blobFiles = Directory.Exists(blobsDir)
            ? Directory.GetFiles(blobsDir, "*", SearchOption.AllDirectories).ToList()
            : new List<string>();
        Assert.Single(blobFiles);
    }

    [Fact]
    public async Task SaveAsync_InterruptedWrite_LeavesNoBlobInBlobsDirectory()
    {
        var store = NewStore();
        var marker = _marker + "-interrupted";

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(new ThrowingStream(), new FileMetadata("expenses", "test", entityId: null, createdBy: marker), CancellationToken.None));

        // No blob may exist under blobs/ — the directory not existing at all is fine.
        var blobsDir = Path.Combine(_root, "blobs");
        if (Directory.Exists(blobsDir))
        {
            Assert.Empty(Directory.GetFiles(blobsDir, "*", SearchOption.AllDirectories));
        }

        // No files row was inserted for this test's marker.
        await using (var verifyContext = await _fixture.Factory.CreateDbContextAsync())
        {
            var count = await verifyContext.Database
                .SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM files WHERE created_by = {0}", marker)
                .SingleAsync();
            Assert.Equal(0L, count);
        }
    }

    [Fact]
    public async Task OpenAsync_MissingBlob_ThrowsMissingBlobException()
    {
        var store = NewStore();
        // A 64-char hex sha with NO corresponding blob file on disk.
        var fakeSha = "ab" + new string('c', 62);

        await using (var context = await _fixture.Factory.CreateDbContextAsync())
        {
            // LAST_INSERT_ID() is connection-scoped: hold one connection explicitly
            // open across the INSERT and the id read so both run on the same MySQL session.
            var conn = context.Database.GetDbConnection();
            var openedByUs = conn.State != ConnectionState.Open;
            if (openedByUs) await conn.OpenAsync();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO files (sha256, byte_size, module, entity_type, created_at, created_by) " +
                                  "VALUES (@p0, @p1, @p2, @p3, @p4, @p5)";
                var p0 = cmd.CreateParameter();
                p0.ParameterName = "@p0";
                p0.Value = fakeSha;
                cmd.Parameters.Add(p0);
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@p1";
                p1.Value = 1L;
                cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@p2";
                p2.Value = "expenses";
                cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter();
                p3.ParameterName = "@p3";
                p3.Value = "test";
                cmd.Parameters.Add(p3);
                var p4 = cmd.CreateParameter();
                p4.ParameterName = "@p4";
                p4.Value = DateTime.UtcNow;
                cmd.Parameters.Add(p4);
                var p5 = cmd.CreateParameter();
                p5.ParameterName = "@p5";
                p5.Value = _marker;
                cmd.Parameters.Add(p5);
                await cmd.ExecuteNonQueryAsync();

                int id;
                using (var sel = conn.CreateCommand())
                {
                    sel.CommandText = "SELECT LAST_INSERT_ID()";
                    id = Convert.ToInt32(await sel.ExecuteScalarAsync());
                }

                Assert.True(id > 0);

                var ex = await Assert.ThrowsAsync<MissingBlobException>(
                    () => store.OpenAsync(id, CancellationToken.None));
                Assert.Equal(id, ex.FileId);
            }
            finally
            {
                if (openedByUs) await conn.CloseAsync();
            }
        }
    }

    [Fact]
    public async Task SoftDeleteAsync_KeepsBlobOnDiskAndSetsDeletedAt()
    {
        var store = NewStore();
        var content = Encoding.UTF8.GetBytes($"filestore-softdelete-{Guid.NewGuid():N}".PadRight(4096, '.'));

        var saved = await store.SaveAsync(new MemoryStream(content), new FileMetadata("expenses", "test", entityId: null, createdBy: _marker), CancellationToken.None);

        var blobPath = Path.Combine(_root, "blobs", saved.Sha256[..2], saved.Sha256[2..4], saved.Sha256);
        Assert.True(File.Exists(blobPath));

        await store.SoftDeleteAsync(saved.Id, "test-pause-reason", CancellationToken.None);

        // The blob file must STILL exist on disk — soft delete never deletes blobs.
        Assert.True(File.Exists(blobPath));

        // Verify via a BRAND NEW DbContext: deleted_at set, deleted_reason persisted.
        await using (var verifyContext = await _fixture.Factory.CreateDbContextAsync())
        {
            var conn = verifyContext.Database.GetDbConnection();
            var openedByUs = conn.State != ConnectionState.Open;
            if (openedByUs) await conn.OpenAsync();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT deleted_at, deleted_reason FROM files WHERE id = @p0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = saved.Id;
                cmd.Parameters.Add(p);

                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.False(reader.IsDBNull(0));
                Assert.Equal("test-pause-reason", reader.GetString(1));
            }
            finally
            {
                if (openedByUs) await conn.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Returns ~512 real bytes on the FIRST read, then throws IOException. The SUT calls
    /// ReadAsync(byte[], CancellationToken), whose default implementation funnels into the
    /// synchronous Read — so overriding only sync Read is sufficient to simulate an
    /// interrupted upload mid-stream.
    /// </summary>
    private sealed class ThrowingStream : Stream
    {
        private readonly byte[] _firstChunk = new byte[512];
        private bool _firstReadDone;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_firstReadDone)
            {
                _firstReadDone = true;
                Buffer.BlockCopy(_firstChunk, 0, buffer, offset, _firstChunk.Length);
                return _firstChunk.Length;
            }

            throw new IOException("simulated interruption");
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

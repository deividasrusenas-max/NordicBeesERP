#!/bin/bash
cd "/Users/deividasru/Projects/ERP DEV/NordicBeesERP"
dotnet build 2>&1
echo "BUILD_EXIT_CODE:$?"

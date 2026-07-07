#!/bin/bash
cd /Users/deividasru/Projects/NordicBeesERP
dotnet build 2>&1 | grep ": error "

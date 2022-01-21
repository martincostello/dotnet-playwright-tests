#! /usr/bin/env pwsh
dotnet test --configuration Release --output ./artifacts --logger "console;verbosity=detailed"

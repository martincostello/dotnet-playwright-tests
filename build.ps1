#! /usr/bin/pwsh
dotnet test --configuration Release --output ./artifacts --logger "console;verbosity=detailed"

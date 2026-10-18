# Start the API and the browser client together from the project root.
$ErrorActionPreference = 'Stop'

# The web app runs migrations on startup, so SQL Server must be available first.
Write-Host 'Starting Bill Splitter at http://localhost:5080/' -ForegroundColor Cyan
Write-Host 'The client is served by the same ASP.NET process as the API.' -ForegroundColor DarkGray

dotnet run --urls 'http://localhost:5080'

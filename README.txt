VS Code quick run:
  dotnet restore InvoiceApp.csproj
  dotnet build InvoiceApp.csproj
  dotnet run --project InvoiceApp.csproj

If MSB1011 appears (multiple project files), delete any temporary '*_wpftmp.csproj' and run the commands above with explicit csproj path.

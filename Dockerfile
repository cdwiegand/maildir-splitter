FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY . .
RUN dotnet build maildir-splitter.csproj -c Release -o /app

# final version
FROM mcr.microsoft.com/dotnet/runtime:3.1 AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet","/app/maildir-splitter.dll"]

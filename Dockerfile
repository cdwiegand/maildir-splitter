FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY . .
RUN dotnet build maildir-splitter.csproj -c Release -o /app

# final version
FROM mcr.microsoft.com/dotnet/runtime:3.1 AS final
WORKDIR /app
COPY --from=build /app .
RUN touch /app/log.log
RUN echo "tail -f /app/log.log" >> /entrypoint.sh; chmod a+rx /entrypoint.sh

# install cron
RUN apt-get update && apt-get install -y cron && apt-get clean && rm -rf /var/lib/apt/lists/*
# create crontab entry
RUN (crontab -l -u root; echo "*/5 * * * * dotnet /app/maildir-splitter.dll") | crontab

ENTRYPOINT /entrypoint.sh

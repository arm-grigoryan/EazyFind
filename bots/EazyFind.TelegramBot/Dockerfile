FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY bots/EazyFind.TelegramBot/EazyFind.TelegramBot.csproj ./bots/EazyFind.TelegramBot/
RUN dotnet restore bots/EazyFind.TelegramBot/EazyFind.TelegramBot.csproj

# Copy the rest and build + publish
COPY . .
RUN dotnet build bots/EazyFind.TelegramBot/EazyFind.TelegramBot.csproj -c Release -o /app/build
RUN dotnet publish bots/EazyFind.TelegramBot/EazyFind.TelegramBot.csproj -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EazyFind.TelegramBot.dll"]

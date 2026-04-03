FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["WhatsAppWebhook.csproj", "./"]
RUN dotnet restore "WhatsAppWebhook.csproj"

COPY . .
RUN dotnet publish "WhatsAppWebhook.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "WhatsAppWebhook.dll"]

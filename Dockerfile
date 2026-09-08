# ---- Etapa de compilación ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copian primero solo los .csproj para aprovechar el cache de capas de Docker: mientras
# las dependencias no cambien, "dotnet restore" no se vuelve a ejecutar en cada build.
COPY MBOS.Hub.CheckInvoice.sln ./
COPY CheckInvoice.Web/CheckInvoice.Web.csproj CheckInvoice.Web/
COPY CheckInvoice.Application/CheckInvoice.Application.csproj CheckInvoice.Application/
COPY CheckInvoice.Infrastructure/CheckInvoice.Infrastructure.csproj CheckInvoice.Infrastructure/
COPY CheckInvoice.core/CheckInvoice.core.csproj CheckInvoice.core/
RUN dotnet restore CheckInvoice.Web/CheckInvoice.Web.csproj

COPY . .
RUN dotnet publish CheckInvoice.Web/CheckInvoice.Web.csproj -c Release -o /app/publish --no-restore

# ---- Etapa de ejecución ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ScottPlot (los gráficos del Reporte Financiero) usa SkiaSharp para renderizar, que en Linux
# necesita fontconfig/freetype instalados aparte del runtime de .NET — sin esto,
# GenerateFinancialDashboardPdf lanza DllNotFoundException al primer intento de generar un gráfico.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "CheckInvoice.Web.dll"]

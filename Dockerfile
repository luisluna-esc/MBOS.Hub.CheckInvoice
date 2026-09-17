FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MBOS.Hub.CheckInvoice.sln ./
COPY CheckInvoice.Web/CheckInvoice.Web.csproj CheckInvoice.Web/
COPY CheckInvoice.Application/CheckInvoice.Application.csproj CheckInvoice.Application/
COPY CheckInvoice.core/CheckInvoice.core.csproj CheckInvoice.core/
COPY CheckInvoice.Infrastructure/CheckInvoice.Infrastructure.csproj CheckInvoice.Infrastructure/
RUN dotnet restore CheckInvoice.Web/CheckInvoice.Web.csproj

COPY CheckInvoice.Web/ CheckInvoice.Web/
COPY CheckInvoice.Application/ CheckInvoice.Application/
COPY CheckInvoice.core/ CheckInvoice.core/
COPY CheckInvoice.Infrastructure/ CheckInvoice.Infrastructure/

RUN dotnet publish CheckInvoice.Web/CheckInvoice.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CheckInvoice.Web.dll"]

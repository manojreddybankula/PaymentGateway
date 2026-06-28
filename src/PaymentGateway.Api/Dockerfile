FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PaymentGateway.sln .
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj src/PaymentGateway.Api/
COPY src/PaymentGateway.Domain/PaymentGateway.Domain.csproj src/PaymentGateway.Domain/
COPY src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj src/PaymentGateway.Infrastructure/
COPY src/PaymentGateway.Service/PaymentGateway.Service.csproj src/PaymentGateway.Service/

RUN dotnet restore src/PaymentGateway.Api/PaymentGateway.Api.csproj

COPY src/ src/

RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
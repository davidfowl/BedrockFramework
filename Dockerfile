FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Bedrock.Framework ./Bedrock.Framework
COPY ServerApplication ./ServerApplication/
COPY ClientApplication ./ClientApplication/
COPY Certs ./Certs/
COPY DistributedApplication ./DistributedApplication/
COPY DistributedApplication.ServiceRegistry ./DistributedApplication.ServiceRegistry/
COPY BedrockTransports.sln ./
RUN dotnet restore

RUN dotnet publish ServerApplication -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

ENTRYPOINT ["./ServerApplication"]
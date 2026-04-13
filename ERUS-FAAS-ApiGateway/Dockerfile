#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

# Step 1: Base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 4443

# Install ICU while still running as root
RUN apk add --no-cache icu-libs

# Create a non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Step 2: Restore and publish(build) the application
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Erus.Faas.ApiGateway/Erus.Faas.ApiGateway.csproj", "Erus.Faas.ApiGateway/"]
COPY ["nuget.config", "."]

RUN sh -c "$(curl -fsSL https://aka.ms/install-artifacts-credprovider.sh)"
RUN --mount=type=secret,id=nuget_feed_endpoints \
    export VSS_NUGET_EXTERNAL_FEED_ENDPOINTS=$(cat /run/secrets/nuget_feed_endpoints) && \
    dotnet restore "Erus.Faas.ApiGateway/Erus.Faas.ApiGateway.csproj"

COPY . .
WORKDIR "/src/Erus.Faas.ApiGateway"
RUN dotnet build "Erus.Faas.ApiGateway.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Erus.Faas.ApiGateway.csproj" -c Release -o /app/publish --no-restore

# Step 3: Final image setup for runtime
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create the folder and assign ownership to the non-root user
RUN mkdir -p /app && chown -R appuser:appgroup /app

# Switch to non-root user after installing packages
USER appuser

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENTRYPOINT ["dotnet", "Erus.Faas.ApiGateway.dll"]
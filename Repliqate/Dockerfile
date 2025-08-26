FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Install clang and other required dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        cmake \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY ["Repliqate.csproj", "./"]
RUN dotnet restore "Repliqate.csproj"
COPY . .
WORKDIR "/src/Repliqate"
RUN dotnet build "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Repliqate"]


FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG GIT_COMMIT=unknown
ARG ARCH=amd64
ARG OS=linux
ARG RESTIC_VER=0.18.0

# Install clang and other required dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        cmake \
		bzip2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY ["Repliqate/Repliqate.csproj", "Repliqate/"]
RUN dotnet restore "Repliqate/Repliqate.csproj"
COPY . .
WORKDIR "/src/Repliqate"
RUN dotnet build "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:InformationalVersion=$VERSION /p:SourceRevisionId=$GIT_COMMIT

RUN echo "Directory contents:" && ls -la /src/Repliqate

# Extract restic binary
RUN mkdir -p /tmp/tools && \
    cd /tmp/tools && \
    wget https://github.com/restic/restic/releases/download/v${RESTIC_VER}/restic_${RESTIC_VER}_${OS}_${ARCH}.bz2 && \
    bunzip2 -c restic_${RESTIC_VER}_${OS}_${ARCH}.bz2 > restic && \
    chmod +x restic


FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
ARG GIT_COMMIT=unknown
RUN dotnet publish "./Repliqate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:InformationalVersion=$VERSION /p:SourceRevisionId=$GIT_COMMIT

FROM mcr.microsoft.com/dotnet/runtime:9.0
ARG ARCH=amd64
ARG OS=linux
ARG RESTIC_VER=0.18.0

# Install tzdata
RUN apt-get update && \
    apt-get install -y tzdata && \
    rm -rf /var/lib/apt/lists/*

# Allow setting timezone via ENV
ENV TZ=UTC
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy over the app
WORKDIR /app
COPY --from=publish /app/publish .

# Create tools directory and copy the extracted restic binary and symlink so it's part of the PATH
RUN mkdir -p /opt/repliqate/tools
COPY --from=build /tmp/tools/restic /opt/repliqate/tools/
RUN ln -s /opt/repliqate/tools/restic /usr/local/bin/restic

ENTRYPOINT ["dotnet", "Repliqate.dll"]

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["QLKTX/QLKTX.csproj", "QLKTX/"]
RUN dotnet restore "QLKTX/QLKTX.csproj"
COPY . .
WORKDIR "/src/QLKTX"
RUN dotnet build "QLKTX.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "QLKTX.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QLKTX.dll"]


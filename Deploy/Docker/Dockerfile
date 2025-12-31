# ============== Build Stage (.NET 8 LTS) ==============
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DcMateH5Api.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish DcMateH5Api.csproj -c Release -o /app/publish

# ============== Runtime Stage (.NET 8 LTS) ==============
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "DcMateH5Api.dll"]

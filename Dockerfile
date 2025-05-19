FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Codecon.Api/Codecon.Api.csproj", "src/Codecon.Api/"]
RUN dotnet restore "src/Codecon.Api/Codecon.Api.csproj"
COPY . .
WORKDIR "/src/src/Codecon.Api"
RUN dotnet build "Codecon.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Codecon.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Codecon.Api.dll"] 
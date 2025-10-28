# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0-preview

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGET_PROJECT=src/Web/IPTVGuideDog.Web.csproj
ARG TARGET_ASSEMBLY=IPTVGuideDog.Web
WORKDIR /source

COPY IPTVGuideDog.sln ./
COPY src/Web/IPTVGuideDog.Web.csproj src/Web/
COPY src/SocketHost/IPTVGuideDog.SocketHost.csproj src/SocketHost/
COPY src/Shared/IPTVGuideDog.Domain.csproj src/Shared/
RUN dotnet restore IPTVGuideDog.sln

COPY . .
RUN dotnet publish ${TARGET_PROJECT} -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
ARG TARGET_ASSEMBLY=IPTVGuideDog.Web
WORKDIR /app
COPY --from=build /app/publish ./
RUN ln -s ${TARGET_ASSEMBLY}.dll service.dll
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "service.dll"]

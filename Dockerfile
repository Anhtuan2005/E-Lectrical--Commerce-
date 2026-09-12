FROM node:22-alpine AS assets
WORKDIR /assets
COPY package.json package-lock.json ./
RUN npm ci
COPY ClientAssets ./ClientAssets
COPY scripts ./scripts
COPY wwwroot ./wwwroot
RUN npm run build:assets

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EcommerceApp.csproj ./
RUN dotnet restore EcommerceApp.csproj
COPY . .
COPY --from=assets /assets/wwwroot ./wwwroot
RUN dotnet publish EcommerceApp.csproj --configuration Release --no-restore --output /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p App_Data/DataProtectionKeys logs wwwroot/uploads && chown -R $APP_UID App_Data logs wwwroot/uploads
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EcommerceApp.dll"]

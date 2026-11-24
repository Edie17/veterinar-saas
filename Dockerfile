FROM node:22-bookworm-slim AS frontend
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run typecheck && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY api/ api/
RUN dotnet restore api/src/Veterinar.Api/Veterinar.Api.csproj --locked-mode
RUN dotnet publish api/src/Veterinar.Api/Veterinar.Api.csproj -c Release --no-restore -o /out
COPY --from=frontend /src/client/dist /out/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /out .
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Veterinar.Api.dll"]

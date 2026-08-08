# ---- 1) Frontend build (Vite/React) ----
FROM node:20-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- 2) Backend publish (ASP.NET Core 8) with the built SPA in wwwroot ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src/backend
COPY backend/ ./
COPY --from=frontend /src/frontend/dist /src/backend/src/DocGenerator.Api/wwwroot
RUN dotnet publish src/DocGenerator.Api/DocGenerator.Api.csproj -c Release -o /app/publish

# ---- 3) Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# PORT تُضبط تلقائيًا بواسطة Render/المضيف؛ البديل 8080 إن غاب.
RUN echo '#!/bin/sh\nexec dotnet DocGenerator.Api.dll --urls "http://0.0.0.0:${PORT:-8080}"' > /entrypoint.sh \
    && chmod +x /entrypoint.sh
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]

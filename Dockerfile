# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY HomeFridgev1/*.csproj ./HomeFridgev1/
RUN dotnet restore HomeFridgev1/HomeFridgev1.csproj

# Copy everything else and build
COPY HomeFridgev1/ ./HomeFridgev1/
RUN dotnet publish HomeFridgev1/HomeFridgev1.csproj -c Release -o out

# Stage 2: Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose port (Render binds to PORT env var, ASP.NET Core 8 binds to 8080 by default)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "HomeFridgev1.dll"]

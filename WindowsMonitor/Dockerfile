
# windows/Dockerfile

# Use a Windows Server Core image with .NET Framework installed
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022

# Set the working directory
WORKDIR /app

# Copy the service executable and all required files to the container
COPY bin/Debug/ .

# Install the service using InstallUtil
RUN C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /i WindowsMonitor.exe

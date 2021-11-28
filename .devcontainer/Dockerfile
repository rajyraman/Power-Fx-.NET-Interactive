# See here for image contents: https://github.com/microsoft/vscode-dev-containers/tree/v0.191.0/containers/dotnetcore/.devcontainer/base.Dockerfile

FROM mcr.microsoft.com/vscode/devcontainers/dotnet:6.0

# [Optional] Uncomment this section to install additional OS packages.
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends libicu-dev

# [Optional] Uncomment this line to install global node packages.
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1


ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
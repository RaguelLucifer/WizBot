dotnet pack "src\WizBot\WizBot.csproj" -c "Release" -o "../../artifacts" --no-build --version-suffix "$Env:BUILD" /p:BuildNumber="$Env:BUILD" /p:IsTagBuild="$Env:APPVEYOR_REPO_TAG"
if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
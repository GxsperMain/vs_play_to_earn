dotnet run --project ./CakeBuild/CakeBuild.csproj -- "$@"
rm -rf "$VINTAGE_STORY/Mods/playtoearn"
cp -r ./Releases/playtoearn "$VINTAGE_STORY/Mods/playtoearn"
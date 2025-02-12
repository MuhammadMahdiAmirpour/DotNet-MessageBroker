🌱 **Smart Plugin Integration Guide** 🌱

<div align="center">

![Coding Cat](https://media.giphy.com/media/LmNwrBhejkK9EFP504/giphy.gif)
<hr>
*"Good developers copy, great developers automate"*

</div>

---

## 🛠️ **Automatic Plugin Setup** 🛠️

### 1. **Edit Consumer Project File**
```xml
<!-- MessageBroker.Consumer.App.csproj -->
<ItemGroup>
  <!-- Add plugin project references -->
  <ProjectReference Include="..\MessageBroker.Plugins.MyCustomPlugin\MessageBroker.Plugins.MyCustomPlugin.csproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    <SkipGetTargetFrameworkProperties>true</SkipGetTargetFrameworkProperties>
  </ProjectReference>
</ItemGroup>

<Target Name="CopyPlugins" AfterTargets="Build">
  <PropertyGroup>
    <PluginsDir>$(OutputPath)plugins</PluginsDir>
  </PropertyGroup>
  
  <ItemGroup>
    <PluginBinaries Include="..\MessageBroker.Plugins.*\bin\$(Configuration)\net9.0\*.dll" />
  </ItemGroup>

  <MakeDir Directories="$(PluginsDir)" />
  <Copy SourceFiles="@(PluginBinaries)" 
        DestinationFolder="$(PluginsDir)" 
        OverwriteReadOnlyFiles="true" />
</Target>
```

### 2. **Solution Structure**
```bash
Solution/
├── MessageBroker.Consumer.App/
│   └── 📄 ConsumerApp.csproj (modified)
└── MessageBroker.Plugins.MyCustomPlugin/
    └── 📄 MyCustomPlugin.cs
```

---

## 🌟 **How It Works** 🌟

1. **Automatic Discovery**  
   Finds all plugin projects matching `MessageBroker.Plugins.*` pattern

2. **Smart Copying**  
   Copies built DLLs to consumer's `plugins` directory on every build

3. **Clean Integration**  
   `ReferenceOutputAssembly=false` keeps your dependencies clean

---

## 🚀 **Development Workflow** 🚀

1. Create new plugin project
```bash
dotnet new classlib -n MessageBroker.Plugins.MyPlugin
```

2. Add to consumer's project file
```xml
<ProjectReference Include="..\MessageBroker.Plugins.MyPlugin\MessageBroker.Plugins.MyPlugin.csproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

3. Build and run - plugins auto-deploy!
```bash
dotnet build
dotnet run
```

<div align="center">

![Magic](https://media.giphy.com/media/12NUbkX6p4xOO4/giphy.gif)  
*No more manual copying!*

</div>

---

## 💡 **Maintenance Tips** 💡

- Add new plugins by simply including their project references
- All plugins rebuild automatically with solution builds
- Keep plugin directory clean with:  
  ```xml
  <Clean Include="$(PluginsDir)\**" />
  ```
- Supports both Debug and Release configurations

---

<div align="center">

🎉 **Happy Automated Developing!**  
*Your future self will thank you for this setup* 💖

</div>

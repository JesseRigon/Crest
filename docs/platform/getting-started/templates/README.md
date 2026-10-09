# Code Generation Templates

Crest Templates use `dotnet new` template configurations for creating new websites, themes and modules from the command shell.

More information about `dotnet new` can be found at <https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-new>

## Installing the Crest CMS templates

Once the .NET Core SDK has been installed, type the following command to install the templates for creating Crest web applications:

```CMD
dotnet new install Crest.ProjectTemplates@3.0.1
```

This will use the most stable release of Crest. In order to use the latest `main` branch of Crest, the following command can be used:

```CMD
dotnet new install Crest.ProjectTemplates@3.0.1-* --nuget-source https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json  
```

## Create a new website

### From Command Shell (automated way)

#### Generate an Crest CMS Web Application

```CMD
dotnet new occms
```

The above command will use the default options.

You can pass the following CLI parameters for setup options:

```CMD
Crest Cms Web App (C#)
Author: Crest Project
Options:
  -lo|--logger           Configures the logger component.
                             nlog       - Configures NLog as the logger component.
                             serilog    - Configures Serilog as the logger component.
                             none       - Do not use a logger.
                         Default: nlog

  -ov|--platform-version  Specifies which version of Crest packages to use.
                         string - Optional
                         Default: 3.0.1
```

Logging can be ignored with this command:

```CMD
dotnet new occms --logger none
```

#### Generate a modular ASP.NET MVC Core Web Application

```CMD
dotnet new ocmvc  
```

### From Visual Studio (New Project dialog)

The templates can also be used from the New Project dialog in Visual Studio.

### From Visual Studio (manual way)

Fire up Visual Studio, create a new solution file (`.sln`) by creating a new ASP.NET Core Web Application:

![image](../assets/images/templates/platform-screencast-1.gif)

Now that we created a new Web Application we need to add proper dependencies so that this new Web Application be targeted as an Crest application.
Crest can be added through two distinct NuGet meta packages: `Crest.Application.Cms.Core.Targets` and `Crest.Application.Cms.Targets`. For additional information regarding these packages, please refer to [this link](../starter-recipes.md). You must add one of these NuGet packages in your Web Application.

!!! note
    If you want to use the `preview` packages, [configure the Crest Preview URL in your Package sources](../preview-package-source.md)

![image](../assets/images/templates/platform-screencast-2.gif)

Visual Studio may automatically include Models, Views, and Controllers folders with boilerplate code, depending on the template used to create the web application. To prevent potential conflicts with Crest services, it is advisable to delete these folders.
Finally, we will need to register Crest CMS service in our `Program.cs` file like this:

```csharp
using Crest.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLogHost();

builder.Services
    .AddPlatformCms()
    .AddSetupFeatures("Crest.AutoSetup");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UsePlatform();

app.Run();
```

## Create a new CMS module

### New module from Command Shell (automated way)

#### Module commands

```CMD
dotnet new ocmodulecms
```

The above command will use the default options.

You can pass the following CLI parameters for setup options:

```CMD
Crest Module (C#)
Author: Crest Project
Options:
  -A|--AddPart           Add dependency injection for part in Program.cs. If PartName is not provided, default name will be used
                         bool - Optional
                         Default: false / (*) true

  -P|--PartName          Add all files required for a part
                         string - Optional
                         Default: MyTest

  -ov|--platform-version  Specifies which version of Crest packages to use.
                         string - Optional
                         Default: 3.0.1
```

```CMD
dotnet new ocmodulecms -n ModuleName.Crest

dotnet new ocmodulecms -n ModuleName.Crest --AddPart true

dotnet new ocmodulecms -n ModuleName.Crest --AddPart true --PartName Test 
```

!!! note
    `Part` is appended automatically to the end of the supplied `PartName`.

### New module from Visual Studio (manual way)

Fire up Visual Studio, open Crest solution file (`.sln`), select `Crest.Modules` folder, right click and select "add --> new project" and create a new .NET Standard Class Library:

![image](../assets/images/templates/38450533-6c0fbc98-39ed-11e8-91a5-d26a1105b91a.png)

For marking this new Class Library as an Crest Module, we will now need to reference `Crest.Module.Targets` NuGet package.

!!! note
    If you want to use the `preview` packages, [configure the Crest Preview URL in your Package sources](../preview-package-source.md)

Each of these `*.Targets` NuGet packages are used to mark a Class Library as a specific Crest functionality.  
`Crest.Module.Targets` is the one we are interested in for now.  
We will mark our new Class Library as a module by adding `Crest.Module.Targets` as a dependency.  
For doing so you will need to right click on `MyModule.Crest` project and select "Manage NuGet Packages" option.  
To find the packages in Nuget Package Manager you will need to check "include prerelease" and make sure you have Crest feed that we added earlier selected.  
Once you have found it, click on the Install button on the right panel next to Version : Latest prerelease x.x.x.x

![image](../assets/images/templates/38450558-f4b83098-39ed-11e8-93c7-0fd9e5112dff.png)

Once done, your new module will look like this:

![image](../assets/images/templates/38450628-31c8e2b0-39ef-11e8-9de7-c15f0c6544c5.png)

For Crest to identify this module it will now require a `Manifest.cs` file. Here is an example of that file:

```csharp
using Crest.Modules.Manifest;

[assembly: Module(
    Name = "TemplateModule.Crest",
    Author = "The Crest Team",
    Website = "http://orchardproject.net",
    Version = "0.0.1",
    Description = "Template module."
)]

```

For this module to start, we now will need to add a `Startup.cs` file to our new module. See this file as an example:  
[`Crest.Templates.Cms.Module/Startup.cs`](https://github.com/OrchardCMS/OrchardCore/tree/dev/src/Templates/OrchardCore.ProjectTemplates/content/OrchardCore.Templates.Cms.Module/Startup.cs)

Last step is to add our new module to the `Crest.Cms.Web` project as a reference for including it as part as our website modules. After that, you should be all set for starting building your custom module. You can refer to our [template module](https://github.com/OrchardCMS/OrchardCore/tree/dev/src/Templates/OrchardCore.ProjectTemplates/content/OrchardCore.Templates.Cms.Module/) for examples of what's basically needed normally.

## Create a new theme

### New theme From Command Shell (automated way)

#### Theme commands

```CMD
dotnet new octheme -n "ThemeName.Crest"
```

### New theme from Visual Studio (manual way)

Should be the same procedure as with modules but instead, we need to reference `Crest.Theme.Targets` and the `Manifest.cs` file differs slightly:

```csharp
using Crest.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "TemplateTheme.Crest",
    Author = "The Crest Team",
    Website = "https://orchardproject.net",
    Version = "0.0.1",
    Description = "The TemplateTheme."
)]
```

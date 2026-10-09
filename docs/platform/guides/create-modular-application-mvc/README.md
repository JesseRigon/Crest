# Creating a modular ASP.NET Core application

## What you will build

You will build a modular ASP.NET Core MVC web application similar to the sample "Hello World" application included with Crest. It includes a web application and a module. The web application provides the layout while the module registers a route and responds to homepage requests. You can refer to the following projects in [Crest](https://github.com/OrchardCMS/OrchardCore) for more information.

- src/Crest.Mvc.Web
- src/Crest.Modules/Crest.Mvc.HelloWorld

## What you will need

- The current version of the .NET SDK. You can download it from here <https://dotnet.microsoft.com/download>.
- A text editor and a terminal where you can run dotnet CLI commands.

## Creating an Crest site and module

There are different ways to create sites and modules for Crest. You can learn more about them [here](../../getting-started/templates/README.md).

In this guide we will use our [Code Generation Templates](../../getting-started/templates/README.md). You can install the latest stable release of the templates using this command:

```dotnet new install Crest.ProjectTemplates@3.0.1-*```

!!! note
    To use the development branch of the template add `--nuget-source https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json`

Create an empty folder, called `Crest.Mvc`, that will contain our projects. Open a terminal, navigate to that folder and run the following command to create the web application:

```dotnet new ocmvc -n Crest.Mvc.Web```

Next, create the "Hello World" module.

```dotnet new ocmodulemvc -n Crest.Mvc.HelloWorld```

!!! important
    Add a project reference to the web application that points to the module.

```dotnet add Crest.Mvc.Web reference Crest.Mvc.HelloWorld```

Optionally, you can add a solution file that references both the web application and module in case you want to open a solution in Visual Studio.

```
dotnet new sln -n Crest.Mvc
dotnet sln add Crest.Mvc.Web\Crest.Mvc.Web.csproj
dotnet sln add Crest.Mvc.HelloWorld\Crest.Mvc.HelloWorld.csproj
```

## Testing the resulting application

From the `Crest.Mvc` root folder containing both projects, run the following command to start the web application:

`dotnet run --project .\Crest.Mvc.Web\Crest.Mvc.Web.csproj`

!!! note
    If you are using the development branch of the templates, run `dotnet restore .\MySite\MySite.csproj --source https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json` before running the application

Your application should now be running and listening on the following ports:

```
Now listening on: https://localhost:5001
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

Open a browser and navigate to <https://localhost:5001/Crest.Mvc.HelloWorld/Home/Index>. It should display __Hello from Crest.Mvc.HelloWorld__.

> The Layout is from the main web application project, while the controller, action and view are from the module project.

## Registering a custom route

By default, all routes in modules follow the pattern `{area}/{controller}/{action}`, where `{area}` is the name of the module. We will change the route of the view in the module to respond to homepage requests.

In the `Startup.cs` file of `Crest.Mvc.HelloWorld`, add a custom route in the `Configure()` method.

```csharp
    routes.MapAreaControllerRoute(
        name: "Home",
        areaName: "Crest.Mvc.HelloWorld",
        pattern: "",
        defaults: new { controller = "Home", action = "Index" }
    );
```

You can also change the `Index.cshtml` file in the module's `Views` -> `Home` folder so that it displays __Hello World__ similar to the project in Crest.

```html
<h1>Hello World</h1>
```

Restart the application and navigate to the homepage at <https://localhost:5001> to display the __Hello World__ message.

## Summary

You just created a modular ASP.NET Core MVC web application using Crest. It includes a web application that supplies the layout and a custom module that responds to homepage requests.

## Tutorial

<https://www.youtube.com/watch?v=LoPlECp31Oo>

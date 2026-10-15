using Grove.Components.Modules;

// Loaded at boot whatever shell the document serves: the host's boot path and the services
// registered before the WASM container is sealed. The admin UI is lazy (Grove.AdminTheme.Client).
[assembly: GroveEager]

// Loads the Facebook SDK once through the module's own endpoints (the tenant's app id, version
// and init parameters in init.js, the culture's SDK in sdk.{culture}.js), then parses the
// plugin elements under one element. docs/widgets.md, the Facebook widgets.
let loading;

function script(src) {
    return new Promise((resolve, reject) => {
        const element = document.createElement('script');
        element.src = src;
        element.async = true;
        element.defer = true;
        element.crossOrigin = 'anonymous';
        element.onload = resolve;
        element.onerror = reject;
        document.head.appendChild(element);
    });
}

function load(culture) {
    if (window.FB) {
        return Promise.resolve();
    }

    if (!loading) {
        loading = (async () => {
            await script('Grove.Integrations.Facebook/sdk/init.js');
            const ready = new Promise(resolve => {
                const init = window.fbAsyncInit;
                window.fbAsyncInit = () => {
                    if (init) {
                        init();
                    }
                    resolve();
                };
            });
            const locale = (culture || 'en-US').replace('-', '_');
            await script('Grove.Integrations.Facebook/sdk/sdk.' + locale + '.js');
            await ready;
        })();
    }

    return loading;
}

export async function parse(element, culture) {
    await load(culture);
    if (window.FB && window.FB.XFBML) {
        window.FB.XFBML.parse(element);
    }
}

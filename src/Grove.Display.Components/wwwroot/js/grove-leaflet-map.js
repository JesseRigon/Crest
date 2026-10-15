// The Leaflet map behind LeafletMap (OpenStreetMap tiles, no API key): one map per element, a
// marker at the point, and a click that places the point and tells .NET.
import * as L from '../leaflet/leaflet-src.esm.js';

const maps = new WeakMap();

function ensureStyles() {
    const href = new URL('../leaflet/leaflet.css', import.meta.url).href;
    if (!document.querySelector(`link[href="${href}"]`)) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }
}

export function create(element, dotnet, latitude, longitude, zoom, readOnly) {
    ensureStyles();
    L.Icon.Default.imagePath = new URL('../leaflet/images/', import.meta.url).href;
    const hasPoint = latitude !== null && longitude !== null;
    const center = hasPoint ? [latitude, longitude] : [20, 0];
    const map = L.map(element, { center, zoom: hasPoint ? zoom : 2 });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        referrerPolicy: 'strict-origin-when-cross-origin',
    }).addTo(map);
    const state = { map, marker: hasPoint ? L.marker(center).addTo(map) : null };
    if (!readOnly) {
        map.on('click', (event) => {
            const lat = Number(event.latlng.lat.toFixed(6));
            const lng = Number(event.latlng.lng.toFixed(6));
            place(state, lat, lng);
            dotnet.invokeMethodAsync('OnPicked', lat, lng);
        });
    }

    maps.set(element, state);
}

function place(state, latitude, longitude) {
    if (state.marker) {
        state.marker.setLatLng([latitude, longitude]);
    } else {
        state.marker = L.marker([latitude, longitude]).addTo(state.map);
    }
}

export function update(element, latitude, longitude) {
    const state = maps.get(element);
    if (state && latitude !== null && longitude !== null) {
        place(state, latitude, longitude);
    }
}

export function dispose(element) {
    const state = maps.get(element);
    if (state) {
        state.map.remove();
        maps.delete(element);
    }
}

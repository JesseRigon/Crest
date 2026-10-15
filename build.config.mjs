export const assetsLookupGlob = "src/*/Assets.json";
export const parcelBundleOutput = "src/Grove.Display.Resources/wwwroot/Scripts/bundle"

export function parcel() {
  return {
    targets: {
      default: {
        engines: {
          browsers: "> 1%, last 4 versions, not dead",
        },
      },
    },
  };
}

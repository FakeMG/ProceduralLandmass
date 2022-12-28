using UnityEngine;

namespace ProceduralLandmassGeneration {
	public static class TextureGenerator {

		public static Texture2D TextureFromColourMap(Color[] colourMap, int width, int height) {
			Texture2D texture = new Texture2D(width, height);
			texture.filterMode = FilterMode.Point;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.SetPixels(colourMap);
			texture.Apply();
			return texture;
		}


		public static Texture2D TextureFromHeightMap(HeightMap heightMap) {
			int width = heightMap.Values.GetLength(0);
			int height = heightMap.Values.GetLength(1);

			Color[] colourMap = new Color[width * height];
			for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					colourMap[y * width + x] = Color.Lerp(Color.black, Color.white,
						Mathf.InverseLerp(heightMap.MinValue, heightMap.MaxValue, heightMap.Values[x, y]));
				}
			}

			return TextureFromColourMap(colourMap, width, height);
		}

	}
}
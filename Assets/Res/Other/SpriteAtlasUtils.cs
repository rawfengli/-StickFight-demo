using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public static class SpriteAtlasUtils
{
    public static Vector2 TexelSizeFromSpriteToSpriteAtlas(Vector2 texelSize, Sprite sprite)
    {
        Vector2 spriteRect = sprite.textureRect.size;

        Vector2 spriteAtlasRect = new Vector2(sprite.texture.width, sprite.texture.height);

        return TexelSizeFromSpriteToSpriteAtlas(texelSize, spriteRect, spriteAtlasRect);
    }
    public static Vector2 TexelSizeFromSpriteToSpriteAtlas(Vector2 texelSize, Vector2 spriteRect, Vector2 spriteAtlasRect)
    {
        float scaleX = spriteRect.x / spriteAtlasRect.x;
        float scaleY = spriteRect.y / spriteAtlasRect.y;

        return new(texelSize.x * scaleX, texelSize.y * scaleY);
    }
}

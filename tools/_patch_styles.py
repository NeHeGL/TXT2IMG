from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
text = p.read_text(encoding="utf-8")
start = text.index('            "Icon" => (')
end = text.index("        // For non-photo styles, strip camera/shot direction")

new_block = r'''            "Icon" => (
                "flat vector app icon",
                "single flat app-icon glyph only, centered on a rounded-square badge, 2 flat solid colors max, huge empty margin, plain solid fill, logo-mark silhouette — not a portrait, not a scene",
                $"{AntiPhotoNegative}, portrait, beauty face close-up, fashion photo, soft shading, detailed skin, 3d render, watercolor, oil painting, claymation, comic book, landscape, scenery, glossy, embossed, illustration scene, realistic clothing",
                false),
            // Unique look = VISIBLE Ben-Day/screentone. Without dots it collapses to smooth digital pop art.
            "Comic Book" => (
                "vintage printed comic book panel",
                "1960s American pulp comic printing, thick India-ink outlines, heavy black spotting, Ben-Day dots and screentone covering every shadow and skin tone, CMYK newsprint, cheap comic-book paper grain, Roy Lichtenstein tipple texture, crosshatched ink — the dots must be obvious",
                $"{AntiPhotoNegative}, smooth digital gradients, soft airbrush, painterly blending, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon, saturday morning cartoon, smooth CG, pastel, anime, clean vector, no tipple, photoreal skin, green skin, alien skin",
                false),
            "Cel-Shaded" => (
                "Wind Waker cel shading",
                "Nintendo GameCube Wind Waker look, soft rounded toon forms, toy-like proportions, thin clean outlines, flat cheerful colors, soft-edged shadow bands only, bright sunny palette — cute GameCube cartoon, not comic ink",
                $"{AntiPhotoNegative}, comic book, comic ink, tipple, Ben-Day, crosshatching, deep blacks, high contrast pulp, Borderlands, gritty, PBR gloss, realistic proportions, watercolor, oil painting, claymation fingerprints, pixel art",
                false),
            "Anime" => (
                "anime key visual",
                "Japanese TV anime still frame, clean manga linework, soft cel-shaded flats, anime eye highlights, studio anime coloring — unmistakably anime",
                $"{AntiPhotoNegative}, western cartoon, comic book, Ben-Day tipple, 3d render, watercolor, oil painting, claymation, pixel art, photoreal skin, live action",
                false),
            "Watercolor" => (
                "watercolor painting on paper",
                "wet translucent watercolor washes, pigment blooms and bleeds, visible cold-press paper tooth, soft edges, delicate brush — clearly watercolor on paper, not a photo",
                $"{AntiPhotoNegative}, 3d render, vector art, flat digital colors, thick oil impasto, comic ink, Ben-Day, claymation, high contrast, deep blacks, neon glow, photoreal, DSLR",
                false),
            "Impasto" => (
                "thick impasto oil portrait",
                "heavy sculpted paint ridges on the figure, palette-knife strokes, chunky visible paint loads, tactile painted person — thick paint depicting the subject, not abstract paint blobs alone",
                $"{AntiPhotoNegative}, smooth digital, airbrushed, flat vector, comic ink, claymation, watercolor wash, sharp photo, 3d render, abstract paint still life without person",
                false),
            "Oil Painting" => (
                "classical oil painting",
                "blended oil on canvas, soft classical brushwork, warm gallery light, fine-art oil pigments, subtle canvas weave — museum oil portrait",
                $"{AntiPhotoNegative}, 3d render, vector art, comic book, cartoon, pixel art, claymation, thick wild impasto ridges, neon, flat digital, photograph",
                false),
            "Claymation" => (
                "stop-motion plasticine claymation",
                "Aardman plasticine puppet, matte clay with fingerprints, soft stop-motion set light, muted clay pigments, lumpy handmade clay, miniature clay set — clay through and through, not skin, not fabric, not a photo",
                $"{AntiPhotoNegative}, comic book, comic ink, Ben-Day, tipple, high contrast, deep blacks, pop art, cel shading, bold ink outlines, glossy, smooth CG, pixar, 3d render, vector art, beauty campaign, fashion photography, cosmetics ad photo, real fabric, real skin pores, studio beauty lighting, green skin, alien, DSLR",
                false),
            "Fantasy Art" => (
                "fantasy concept painting",
                "epic fantasy illustration, painterly digital concept art, magical atmosphere, dramatic illustrated light, storybook fantasy painting — not a photo",
                $"{AntiPhotoNegative}, 3d render, vector flat, comic tipple, cartoon outlines, anime eyes, claymation, pixel art, photograph, modern interior photo, product photo",
                false),
            "Pixel Art" => (
                "retro pixel art sprite",
                "chunky 8-bit pixels on every surface, limited NES palette, hard pixel edges, sprite-sheet clarity, no anti-aliasing — entire frame is pixel art",
                $"{AntiPhotoNegative}, smooth, anti-aliased, blurry, 3d render, watercolor, oil painting, claymation, comic tipple, photoreal detail, soft gradients, DSLR",
                false),
            "3D Render" => (
                "Pixar-style 3D render",
                "animated film CG, smooth subsurface shading, soft studio key light, clean rendered materials, friendly dimensional forms, high-quality animation still",
                $"{AntiPhotoNegative}, flat vector, comic ink, watercolor, oil painting, claymation fingerprints, pixel art, sketch, photoreal pores",
                false),
            "Photorealistic" => (
                "photorealistic photograph",
                "natural DSLR photograph, believable lighting, sharp focus, real-world materials and detail",
                "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, vintage sepia, film grain overlay, pixel art, 3d cartoon render, claymation",
                true),
            "Vintage Photo" => (
                "vintage analog photograph",
                "old film photo, soft faded colors, visible film grain, slight sepia warmth, nostalgic analog snapshot",
                "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, digital clean, modern HDR, pixel art, 3d render, claymation",
                true),
            _ => (
                "animated TV cartoon",
                "2D Saturday-morning TV cartoon still, bold clean cartoon outlines, flat bright cel colors, simple cartoon shapes, painted cartoon BG, hand-drawn animation on television — smooth cartoon color, NO Ben-Day dots, NO comic print",
                $"{AntiPhotoNegative}, 3d render, pixar, soft painterly shading, oil paint, watercolor, claymation, comic book, comic print, tipple dots, ben-day dots, screentone, manga, anime eyes, photoreal, pulp comic inking, real skin, product photography",
                false), // Cartoon
        };

'''

# Fix accidental tipple typos -> tipple... wait, tipple should be tipple? 
# CORRECT WORD IS: tipple NO - tipple is wrong. Correct is tipple.
# THE WORD IS tipple? 
# It's H-A-L-F-T-O-N-E
new_block = new_block.replace("tipple", "halftone")

p.write_text(text[:start] + new_block + text[end:], encoding="utf-8")
print("ok", start, end)

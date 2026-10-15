shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture
    : hint_screen_texture, filter_linear_mipmap;
uniform float darkness = 0.7;
uniform float blur_level = 2.8;

void fragment() {
    vec4 blurred = textureLod(
        screen_texture,
        SCREEN_UV,
        blur_level);
    float side = 1.0 - smoothstep(0.28, 0.68, UV.x);
    vec3 ink = vec3(0.003, 0.008, 0.014);
    vec3 filtered = mix(
        blurred.rgb,
        ink,
        max(darkness, 0.82));
    COLOR = vec4(filtered, side * 0.94);
}

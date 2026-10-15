shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture
    : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec4 blurred = textureLod(screen_texture, SCREEN_UV, 3.4);
    float focus = 1.0 - smoothstep(
        0.08,
        0.74,
        length((UV - vec2(0.5)) * vec2(0.82, 1.0)));
    vec3 filtered = mix(
        blurred.rgb,
        vec3(0.002, 0.006, 0.011),
        0.84);
    COLOR = vec4(filtered, 0.8 + focus * 0.1);
}

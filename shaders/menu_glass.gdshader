shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec3 scene = textureLod(screen_texture, SCREEN_UV, 3.8).rgb;
    vec2 p = (UV - vec2(0.5)) * vec2(0.82, 1.0);
    float centre = 1.0 - smoothstep(0.14, 0.8, length(p));
    vec3 tint = mix(vec3(0.015, 0.036, 0.052), vec3(0.026, 0.021, 0.019), UV.x);
    // One blurred optical layer: no opaque card and no sharp ghost text beneath the menu.
    COLOR = vec4(mix(scene, tint, 0.55 + centre * 0.12), 1.0);
}

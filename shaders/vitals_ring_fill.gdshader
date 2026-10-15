shader_type spatial;
render_mode unshaded, cull_disabled, depth_prepass_alpha;

uniform vec4 tint : source_color = vec4(1.0);
uniform float fill_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float glow_energy : hint_range(0.0, 8.0) = 2.0;

void fragment() {
    float progress = fract(UV.x + 0.02);
    if (progress > fill_ratio) {
        discard;
    }
    ALBEDO = tint.rgb;
    EMISSION = tint.rgb * glow_energy;
    ALPHA = tint.a;
}

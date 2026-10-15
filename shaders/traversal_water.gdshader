shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 deep_color : source_color = vec4(0.035, 0.23, 0.29, 1.0);
uniform vec4 crest_color : source_color = vec4(0.36, 0.75, 0.75, 1.0);
varying vec2 water_position;

void vertex() {
    water_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    VERTEX.y += sin(water_position.x * 0.65 + TIME * 0.85) * 0.035
        + sin(water_position.y * 0.9 - TIME * 0.7) * 0.025;
}

void fragment() {
    vec2 p = water_position;
    p += vec2(sin(p.y * 0.43 + TIME * 0.16), sin(p.x * 0.37 - TIME * 0.12)) * 1.2;
    float wave_a = sin(p.x * 1.2 + p.y * 0.72 + TIME * 0.65);
    float wave_b = sin(p.x * 0.85 - p.y * 1.36 - TIME * 0.55);
    float caustic = pow(max(0.0, 1.0 - abs(wave_a + wave_b) * 1.9), 5.0);
    float shimmer = 0.5 + 0.5 * sin(p.y * 0.3 - TIME * 0.4);
    float edge = 1.0 - smoothstep(0.0, 0.025, min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y)));
    ALBEDO = mix(deep_color.rgb, crest_color.rgb, caustic * 0.14 + edge * 0.2 + shimmer * 0.05);
    NORMAL_MAP = normalize(vec3(wave_a * 0.035, wave_b * 0.035, 1.0)) * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 0.32;
    ROUGHNESS = 0.38;
    METALLIC = 0.08;
    EMISSION = crest_color.rgb * caustic * 0.045;
}

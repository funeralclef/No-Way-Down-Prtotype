shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
uniform vec4 flow_color : source_color = vec4(0.2, 0.85, 0.83, 1.0);
void fragment() {
    float chevron = abs(fract(UV.y * 10.0 - abs(UV.x - 0.5) * 1.4 + TIME * 0.75) - 0.5);
    float mark = 1.0 - smoothstep(0.025, 0.055, chevron);
    float edge = 1.0 - smoothstep(0.015, 0.035, min(UV.x, 1.0 - UV.x));
    float fade = smoothstep(0.0, 0.07, UV.y) * (1.0 - smoothstep(0.93, 1.0, UV.y));
    ALBEDO = flow_color.rgb;
    EMISSION = flow_color.rgb * 0.45;
    ALPHA = (0.025 + mark * 0.38 + edge * 0.25) * fade;
}

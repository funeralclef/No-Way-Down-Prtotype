shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled;

uniform vec4 cool_color : source_color = vec4(0.24, 0.82, 0.94, 1.0);
uniform vec4 warm_color : source_color = vec4(1.0, 0.71, 0.45, 1.0);

void fragment() {
    float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 3.4);
    float pearl = 0.5 + 0.5 * sin(UV.y * 7.0 + TIME * 0.35);
    ALBEDO = vec3(0.0);
    EMISSION = mix(cool_color.rgb, warm_color.rgb, pearl * 0.55) * rim * 0.65;
    ALPHA = rim * 0.32;
}

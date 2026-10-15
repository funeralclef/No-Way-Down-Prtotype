shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color;
uniform vec4 accent_color : source_color;
uniform float arena_mix = 0.0;
varying vec3 world_position;

void vertex() {
    world_position =
        (MODEL_MATRIX *
         vec4(VERTEX, 1.0)).xyz;
}

float grid_line(float coordinate, float spacing, float width) {
    float cell = fract(coordinate / spacing);
    float edge = min(cell, 1.0 - cell);
    return 1.0 -
        smoothstep(0.0, width, edge);
}

void fragment() {
    float lane_line =
        grid_line(
            world_position.x,
            mix(6.0, 4.5, arena_mix),
            0.018);
    float beat_line =
        grid_line(
            world_position.z,
            mix(12.0, 7.5, arena_mix),
            0.012);
    float grid =
        max(
            lane_line * 0.42,
            beat_line * 0.26);
    float scan =
        0.5 +
        0.5 *
        sin(
            world_position.z * 0.12 -
            TIME * 0.72);
    float zone_position =
        fract(
            (-world_position.z + 10.0) /
            144.0);
    float light_to_dark =
        1.0 -
        smoothstep(
            0.26,
            0.46,
            zone_position);
    float dark_to_light =
        smoothstep(
            0.66,
            1.0,
            zone_position);
    float zone_light =
        max(
            light_to_dark,
            dark_to_light);
    float surface_light =
        mix(
            0.82,
            1.1,
            zone_light);
    surface_light =
        mix(
            surface_light,
            1.0,
            arena_mix * 0.72);
    ALBEDO =
        base_color.rgb *
        surface_light *
        (0.97 + scan * 0.025) +
        accent_color.rgb *
        grid * 0.014;
    EMISSION =
        accent_color.rgb *
        grid *
        (0.018 + scan * 0.012);
    METALLIC = 0.05;
    ROUGHNESS = 0.76;
}

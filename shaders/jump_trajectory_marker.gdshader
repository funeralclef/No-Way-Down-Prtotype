shader_type spatial;
render_mode unshaded, cull_disabled, depth_prepass_alpha;

varying vec4 marker_tint;

void vertex() {
	marker_tint = INSTANCE_CUSTOM;
}

void fragment() {
	ALBEDO = marker_tint.rgb;
	EMISSION = marker_tint.rgb * 1.8;
	ALPHA = marker_tint.a;
}

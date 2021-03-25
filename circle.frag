#ifdef GL_ES
precision mediump float;
#endif


uniform vec2 u_resolution; // gives current canvas resolution
uniform float u_time; // gives time since start




void main() {
    vec2 normal_pixel_pos = gl_FragCoord.xy/u_resolution; // normalizes the pixel position between 0 and 1
    float time = pow(u_time,2.0);

    float r = 0.1; // circle radius
    float width = 0.019; // border width

    float x0 = (sin(time)*0.3)+0.5+((cos(time/1.0)*0.2));
    float y0 = (cos(time)*0.3)+0.5+((sin(u_time/1.0)*0.2));

    float distance = sqrt(pow(normal_pixel_pos.x-x0,2.0)+pow(normal_pixel_pos.y-y0,2.0));
    
    float color = step(distance, r) *step(r-(width+0.001),distance); // defines when we draw colors

    float outside = step(color,0.1); // gives the inverse of where the circle is
    
    // need two smoothsteps so that the inside does not get colored in
    float fadedborder = 1.0*smoothstep(r+0.1, r-0.2, distance)*smoothstep(r, r,distance);

    gl_FragColor = vec4(((sin(time)*0.5)+0.5)*color,((cos(time)*0.5)+0.5)*color+1.0*fadedborder,((tan(time)*0.5)+0.5)*color+1.0*fadedborder,1);
    // gl_FragColor = vec4(outside);
    
}

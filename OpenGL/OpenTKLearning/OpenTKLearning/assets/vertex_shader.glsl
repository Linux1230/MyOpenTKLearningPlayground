#version 410

in vec3 vertices;
in vec3 colors;

out vec3 fragColor;

void main() {
    fragColor = colors; 

    gl_Position = vec4(vertices, 1.0);
}
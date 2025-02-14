#version 410 core



//The light contains all the values from the light source, how the ambient diffuse and specular values are from the light source.
//This is technically what we were using in the last episode as we were only applying the phong model directly to the light.
struct Light {
    vec3 position;

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

//We create the light and the material struct as uniforms.
uniform Light light;

flat in int flight;

out vec4 color;

void main()
{          
    //Sunlight value
    int sunlight = flight >> 4 & 0xF;
    //Block light value
    int torchlight = flight & 0xF;

    float sunlightIntensity = max(light.position.y * 0.96f + 0.6f, 0.02f);
    float lightIntensity = torchlight + sunlight * sunlightIntensity;

    color = vec4(1,1,0,1) * lightIntensity;
}

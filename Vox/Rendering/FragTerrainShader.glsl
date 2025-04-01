#version 430 core

uniform sampler2DArray texture_sampler;
uniform sampler2DShadow sunlightDepth_sampler;

//The material is a collection of some values that we talked about in the last tutorial,
//some crucial elements to the phong model.
struct Material {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;

    float shininess; //Shininess is the power the specular light is raised to
};

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
uniform Material material;

//We still need the view position.
uniform vec3 viewPos;

uniform vec3 playerPos;
uniform int chunkSize;
uniform vec3 playerMin;
uniform vec3 playerMax;
uniform vec3 targetVertex;
uniform vec3 forwardDir;

in vec4 fblockCenter;
in vec4 fcurPos;
in vec4 flocalHit;

in vec4 fragPosLightSpace;
flat in float fTexLayer;
in vec4 fTargetVertex;
in vec2 ftexCoords;
in vec3 fragPos;
in vec3 fnormal;
in vec3 vertexPos;
in vec4 fColor;
in vec3 fforwardDir;
out vec4 color;


float ShadowCalculation(vec4 fragPosLightSpace)
{
    // Perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    
    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

    // Bias to reduce shadow acne
    float bias = max(0.005 * (1.0 - dot(normalize(fnormal), normalize(light.position - fragPos))), 0.0005);

    // Sample the shadow map using a shadow sampler (automatically compares with current depth)
    float shadow = texture(sunlightDepth_sampler, vec3(projCoords.xy, projCoords.z - bias));

    return shadow;
}  

void main()
{          

    //=========================================
    // Lighting
    //=========================================

    //diffuse 
 // vec3 norm = normalize(fnormal);
 // vec3 lightDir = normalize(light.position - fragPos);
 // float diff = max(dot(norm, lightDir), 0.0);
 // vec3 diffuse = light.diffuse * (diff * material.diffuse); //Remember to use the material here.
 //
 // //specular
 // vec3 viewDir = normalize(viewPos - fragPos);
 // vec3 reflectDir = reflect(-lightDir, norm);
 // float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
 // vec3 specular = light.specular * (spec * material.specular); //Remember to use the material here.
 //
 //


    vec3 norm = normalize(fnormal);
    vec3 lightColor = vec3(1.0);
    //vec3 ambient = 0.15 * lightColor;
   // float ambient = 0.15;

    //Diffuse
    vec3 lightDir = normalize(light.position - fragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = light.diffuse * (diff * material.diffuse); //Remember to use the material here.

    //Specular
    vec3 viewDir = normalize(viewPos - fragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    vec3 specular = light.specular * (spec * material.specular); //Remember to use the material here.

    float sunDot = dot(-lightDir, norm);
    float falloff = dot(vec3(0,1,0), -lightDir);
    float ambient = clamp(0.5 * falloff, 0, 1);
    float value = clamp(ambient + (max(0.0, sunDot) * falloff), 0.1, 1.0);

    // calculate shadow     
    float shadow = ShadowCalculation(fragPosLightSpace); 
    vec3 clr = (vec4(texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)))).rgb;
    vec3 lighting = (ambient + (0.5 - shadow) * (diffuse + specular)) * clr; 
 
  // vec3 result =  value + diffuse + specular;
    vec3 result =  value + diffuse + specular * lighting * 8;

    //Now the result sum has changed a bit, since we now set the objects color in each element, we now dont have to
    //multiply the light with the object here, instead we do it for each element seperatly. This allows much better control
    //over how each element is applied to different objects.

   // float sunlightIntensity = max(light.position.y * 0.05f, 0.02f);
 //  float lightIntensity = sunlightIntensity + sunDot;
 // vec3 result = value + diffuse + specular;


    //=========================================
    // Render block target
    //=========================================

    //target bounds checking
    vec4 minBound = fTargetVertex;
    vec4 maxBound = minBound + vec4(1.01,1.01,1.01,0);

    float gamma = 1.0 / 1.5; 

    // Check if the fragment's position is inside the bounding box
    if ((fragPos.x >= minBound.x && fragPos.x <= maxBound.x &&
        fragPos.y >= minBound.y && fragPos.y <= maxBound.y &&
        fragPos.z >= minBound.z && fragPos.z <= maxBound.z))
    {
        // If inside the bounding box, combine texture with target texture
        vec4 baseTex = vec4(texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)));
        vec4 targetOverlay = vec4(texture(texture_sampler, vec3(ftexCoords.xy, 4)));

        float shadow = ShadowCalculation(fragPosLightSpace); 
       // vec4 c = mix(baseTex, targetOverlay, targetOverlay.a) * vec4(result, 1.0) * vec4(lighting, 1.0);
        vec4 c = mix(baseTex, targetOverlay, targetOverlay.a) * vec4(result, 1.0)* vec4(lighting, 1.0);
        color = pow(c, vec4(gamma));
    
    }
    
    else {
        float shadow = ShadowCalculation(fragPosLightSpace); 
       // vec4 c = (vec4(texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)))) * vec4(result, 1.0) * vec4(lighting, 1.0);
       vec4 c = (vec4(texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)))) * vec4(result, 1.0)* vec4(lighting, 1.0);
       color = pow(c, vec4(gamma));
    }
 }
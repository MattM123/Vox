#version 430 core

uniform sampler2DArray texture_sampler;
uniform sampler2DShadow sunlightDepth_sampler;

//The material is a collection of some values that we talked about in the last tutorial,
//some crucial elements to the phong model.
struct Matt {
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
uniform Matt material;

//We still need the view position.
uniform vec3 viewPos;

uniform vec3 playerPos;
uniform int chunkSize;
uniform vec3 playerMin;
uniform vec3 playerMax;
uniform vec3 targetVertex;
uniform vec3 forwardDir;
uniform int targetTexLayer;

in vec4 fragPosLightSpace;
flat in float fTexLayer;
flat in int fLighting;
in vec4 fTargetVertex;
in vec2 ftexCoords;
in vec3 fragPos;
in vec3 fnormal;
in vec4 fColor;
in vec3 fforwardDir;
out vec4 color;

//Override specular function
#define SPECULAR_FNC specularBeckmann

//Shader data datatype
#line 1 "shadingData.glsl"
#include "lygia\\lighting\\shadingData\\shadingData.glsl"

//2D and 3D Simplex noise
#line 1 "snoise.glsl"
#include "lygia\\generative\\snoise.glsl"

//Specular lighting
#line 1 "cookTorrance.glsl"
#include "lygia\\lighting\\specular\\cookTorrance.glsl"

//Diffuse lighting
#line 1 "diffuse.glsl"
#include "lygia\\lighting\\diffuse.glsl"


float ShadowCalculation(vec4 fragPosLightSpace)
{

    // Perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    
    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

   // if (projCoords.z > 1.0)
   //     return 0.0; 

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

    // Blue component (bits 0-3)           
    float blue = fLighting & 0x000F;

    // Green component (bits 4-7)
    float green = (fLighting & 0x00F0) >> 4;

    // Red component (bits 8-11)
    float red = (fLighting & 0x0F00) >> 8;

    //Normalize color value
    vec3 emissiveColor = vec3(red / 15, green / 15, blue / 15);

    // Sunlight component (bits 12-15)
    float sunlight = (fLighting & 0x0F);
    
   //========================================
   //Shading data 
   //========================================
    vec3 frensel = vec3(0.2);                               //~10% reflectivity for everything
    vec3 norm = normalize(fnormal);                         // Surface normal
    vec3 viewDir = normalize(viewPos - fragPos);            // View direction
    vec3 lightDir = normalize(light.position - fragPos);    // Light direction
    vec3 halfVec = normalize(viewDir + lightDir);           // Halfway vector
    vec3 reflection =  reflect(-viewDir, norm);             // Reflicttion

    float specColor = pow(max(dot(viewDir, reflection), 0.0), material.shininess);
    vec3 lightColor = light.diffuse;

    //Light attenuation
    float dist = length(light.position - fragPos);
   // float attenuation = 3000.0 / (dist * dist); // Inverse square law


    
    //Diffuse shader data setup
    ShadingData shadingData;

    shadingData.N = norm;
    shadingData.V = viewDir;
    shadingData.L = lightDir;
    shadingData.H = halfVec;
    shadingData.R = reflection;

    shadingData.NoV = dot(norm, viewDir);
    shadingData.NoL = dot(norm, lightDir);
    shadingData.NoH = dot(norm, halfVec);

    shadingData.roughness = 0.5;
    shadingData.linearRoughness = shadingData.roughness * shadingData.roughness;
    shadingData.diffuseColor = material.diffuse;
    shadingData.specularColor = material.specular * lightColor;
    shadingData.energyCompensation = (1.0 - shadingData.specularColor) * material.diffuse;

    shadingData.directDiffuse = lightColor * material.diffuse * max(dot(norm, lightDir), 0.0);
    shadingData.directSpecular = lightColor * shadingData.specularColor;
    shadingData.indirectDiffuse = light.ambient * material.diffuse;
    shadingData.indirectSpecular =  material.specular;

     float specular = specColor;
     vec3 diffuse = diffuse(shadingData) * light.diffuse * material.diffuse;
      
    // calculate shadow    
    float shadow = ShadowCalculation(fragPosLightSpace); 

    float lightIntensity = 0.1;


    vec3 lighting = (light.ambient + material.ambient + (diffuse + specular) * (1.0 - shadow)) * (lightIntensity);
    vec3 result = (diffuse + shadingData.specularColor);

    //If block is emissive, render block lighting properly
   if (blue > 0 || red > 0 || green > 0) {
        //Add the emissive color to the blocks lighting value
        lighting = ((light.ambient + material.ambient) + (diffuse + specular) * (1.0 - shadow)) * (lightIntensity) + emissiveColor;
        

        result = vec3(1,1,1) * (lightIntensity + emissiveColor) + diffuse;
      
   }



    //=========================================
    // Render block target
    //=========================================

    //target bounds checking
    vec3 minBound = fTargetVertex.xyz;
    vec3 maxBound = minBound + vec3(1.0,1.0,1.0);
    vec4 applyTex;
    float gamma = 1.0 / 2.2; 


    // Check if the fragment's position is inside the bounding box
    if ((fragPos.x >= minBound.x && fragPos.x <= maxBound.x &&
        fragPos.y >= minBound.y && fragPos.y <= maxBound.y &&
        fragPos.z >= minBound.z && fragPos.z <= maxBound.z))
    {
        // If inside the bounding box, combine texture with target texture
        vec4 baseTex = texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer));
        vec4 targetOverlay = texture(texture_sampler, vec3(ftexCoords.xy, targetTexLayer));
        applyTex = mix(baseTex, targetOverlay, targetOverlay.a) * vec4(result, 1.0) * vec4(lighting, 1.0);  
    }
 
    else 
        applyTex = texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)) * vec4(result, 1.0) * vec4(lighting, 1.0); 


    color = pow(applyTex, vec4(gamma));

 }
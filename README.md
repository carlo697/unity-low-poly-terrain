## What is this?

This project is a procedural terrain generator that uses the marching cubes algorithm and procedural noise textures (using [FastNoise2](https://github.com/Auburn/FastNoise2/commits/NewFastSIMD/)). Built with Unity 2022.2.19.

## Features

1. Large render distance thanks to a LOD system using a quadtree.
2. Biome system using voronoi regions and a simple temperature and precipitation noise textures.
3. Spawning details (objects) over the terrain, like trees.
4. Spawning grass over the terrain.

## To-do

- Add more example biomes.
- Add more example details and grass: trees, rocks, different grass types, flowers.
- An editor inside Unity to create the FastNoise2 noises.
- Improve perfomance of details.
- Improve perfomance of grass.
- Generation of caves and similar structures.
- Better water mesh (maybe using marching squares).
- Editable terrain?

## Images
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/ec007d10-0c30-4d01-a733-287402cb3054)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/7bf08928-99cd-4150-9c45-f93d5bd82016)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/5a97021e-5147-4702-95bf-befb8ad9afdc)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/ac322304-3349-4408-9c40-9d165f5a1ffb)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/d826a55a-3afa-40d9-bb5b-cf385f10ebab)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/4e29f6f5-a1e8-4ba6-ac5d-8c95f019cd41)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/cf5afaeb-390d-4cb3-9ea5-29dfd7c0443c)
![image](https://github.com/carlo697/unity-low-poly-terrain/assets/16585568/d768d322-9692-4382-ad7d-f22ac13cc746)


# Boat Attack Water

Public slack channel: [#boat-attack](https://unity.slack.com/messages/C02M107KQJC/) <br/>
[View this project in Backstage](https://backstage.corp.unity3d.com/catalog/default/component/boat-attack-water) <br/>

This repo contains the Unity package for the water system that is used in the Universal Render Pipeline demo 'Boat Attack'.
The package is under constant development and is not supported in any official capacity.

<img width="1734" alt="image" src="https://user-images.githubusercontent.com/9811576/140533647-cb99836c-64ce-485f-9cce-344f4836212b.png">

## Fork description

Our team needed a solution to visualize the infinite surface of the ocean. Unfortunately, we could not find anything that could give a good picture and high performance. Therefore, it was decided to work with the boat attack water system.

This system looks amazing, but unfortunately, the developer did not provide flexible adjustments, that could allow you to effectively disable heavy effects to able visualize the water on low-end devices.

### What is changed?

This fork is focused on achieving maximum performance on low-end devices. All heavy visual effects and buoyant system were disabled, and left only:
- infinity water plane;
- Gerstner waves;
- normal details;
- probe reflections;
- specular highlights;

### Performance

The system was tested on a Google Nexus (Android) device and reached stable 60+ fps in native resolution.

### How To Use

Just drag & drop Ocean prefab from the Runtime package folder to your scene.

### Issues

Sometimes in Unity Editor reflections on the infinity water plane are not applied, but in the build (on Android at least) it works 
# Changelog
All notable changes to this package will be documented in this file. The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)

## [1.2.0] - 2025-11-20
- Added AgentCrowdPathingAuthoring enable/disable state
- Added new component AgentCrowdDisabled that allows disabling crowd pathing without structural changes
- Fixed regression from 1.1.0 where surface without data was causing type errors

## [1.1.0] - 2025-06-17
- Fixed CrowGroup Grounded comment
- Added CrowGroupAuthoring SetSurface method
- Changed a lot of systems not to run without existing components

## [1.0.3] - 2025-03-01
- Fixed AddGoal properly offset
- Added CrowdObstacleAuthoring the Obstacle property

## [1.0.2] - 2024-08-24
- Fixed width/height change causing exception from gizmos code
- Added new overload AddGoal(float3 min, float3 max)
- Added CrowdSurfaceAuthoring.ApplyData

## [1.0.1] - 2023-12-12
- Fixed ECS CrowdSurface and CrowdGroup cleanup

## [1.0.0] - 2023-11-19
- Package released
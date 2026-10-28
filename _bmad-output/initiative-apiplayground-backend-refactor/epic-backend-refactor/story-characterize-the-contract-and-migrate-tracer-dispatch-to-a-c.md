---
id: 2
type: story
title: "Characterize the contract and migrate tracer dispatch to a controller"
parent: epic-backend-refactor
covers: [CAP-2, CAP-6, CAP-8]
after: []
hitl: false
risk: medium
---

# Characterize the contract and migrate tracer dispatch to a controller

## Description

Capture the current HTTP baseline and deliver the POST /api/tracer/get action as the first thin API-to-Application-to-Infrastructure controller path, preserving outbound and persistence smoke-check outcomes.

## Acceptance Criteria

Verify: TracerEndpointTests and TracerPersistenceSmokeCheckTests pass for valid and invalid dispatch, guard rejection, statuses, and response JSON; the attribute-routed action reaches the existing guarded executor.

## References

- parent — _bmad-output/initiative-apiplayground-backend-refactor/epic-backend-refactor/epic-backend-refactor.md

## Notes

- Risk: this establishes the HTTP pattern and touches a security-guarded action; focused guard, route, outbound, and smoke-check regression coverage is required.

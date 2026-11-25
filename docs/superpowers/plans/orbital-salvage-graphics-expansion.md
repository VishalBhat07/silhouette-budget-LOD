# Orbital Salvage graphics expansion

## Story and visual scope

The technician recovers the disabled tug’s navigation core at the orbital salvage dock. The core points to a relay station on a rocky moon that can reconstruct the tug’s final flight log. The technician boards a salvage shuttle, makes a **7.5-second jump**, and touches down on the station’s exterior pad. A maintenance rover leads them through the airlock to the uplink. The final shot shows the recovered core seated in the console and the relay dish turning toward the sky.

This plan covers the environments, models, materials, lighting, animation, VFX, camera framing, and render deliverables for that storyline. The [research plan](2026-11-22-silhouette-budget-lod.md) documents the triangle-allocation experiments.

### Visual direction

- **Salvage dock.** Warm work lights, worn orange rails, exposed machinery, and dark space behind the tug. Add a shuttle berth just beyond the extraction hatch so the existing sequence leads naturally to departure.
- **Shuttle.** A compact working vehicle with a broad faceted windshield, two offset engine pods, exposed radiator fins, a recessed boarding hatch, and folding landing gear. Use light ceramic panels, dark metal around the engines, and a muted orange stripe that links the shuttle to the salvage crew.
- **Moon relay.** A low station with cylindrical habitats bolted to an angular landing deck. Surround it with grey rock, long shadows, and cool white pad lights. Warm light inside the airlock and relay hall draws the eye toward the uplink.

## 1. Shuttle and the two camera views

### Exterior and chase view

![Dream Chaser concept alongside the International Space Station](https://www.maxongroup.com/assets/public/caas/v1/media/23810/data/d710c3011b387d8147dd3799437ff0b6/landscape_ratio16x9/1920/dream-chaser-berthed-to-iss.jpg)

Use Dream Chaser for compact proportions and station scale. Study the *Bowie* exterior concepts for how separate engine clusters and fins make a ship recognizable in silhouette. Build an original salvage shuttle around the windshield, paired pods, and cargo hatch. [Dream Chaser reference](https://www.planetary.org/space-images/dream-chaser-docked-at) · [Bowie exterior concepts](https://www.artstation.com/artwork/qeOxVa)

Place the chase camera above and slightly to one side of the engines. Keep the shuttle in the lower third, leaving room for the jump aperture and the landing pad. The rear angle should show the silhouette changes as the fins bank and the landing gear unfolds. [EVERSPACE 2 developer images and camera design notes](https://store.steampowered.com/news/posts/?appids=1128920&enddate=1601403747&feed=steam_community_announcements)

### Cockpit and windshield

![Sebastian Meyer’s cockpit concept art for the Bowie](https://images.squarespace-cdn.com/content/v1/528d35eae4b0c2ddaef3ccff/1717523556085-W3AJA5TGE7BJ09Y95WB4/Guardians%2Bof%2Bthe%2Bgalaxy%2Bvol%2B3%2Bconcept%2Bart%2BBowie.jpg?format=1500w)

Use warm instrument light, physical controls, layered consoles, and cool light coming through the glass. Keep the dashboard low. Give the forward view one broad center pane and two angled side panes so the player can see the aperture and the pad lights during the bank. The cockpit glass and frame should match their shape on the outside of the shuttle. [Sebastian Meyer’s Bowie concepts](https://www.sebmeyerart.com/guardians-of-the-galaxy-3) · [Cockpit set photos and finished frames](https://assets.adg.org/media/submissions/2023-11-06_09-34-16/GotG3_Presenation_ADG_V6_C.pdf)

![View of spacecraft hardware and Earth through a framed ISS window](https://www.esa.int/var/esa/storage/images/esa_multimedia/images/2019/09/space_station_view/19686099-2-eng-GB/Space_Station_view.jpg)

Use the Cupola photo for window thickness, foreground framing, and how the horizon reads beyond nearby hardware. Set the cockpit camera at seated eye height. Both cameras follow the same animated shuttle path and show the same moments of departure and landing. [ESA Cupola photograph](https://www.esa.int/ESA_Multimedia/Images/2019/09/Space_Station_view) · [Endeavour flight-deck photograph](https://science.nasa.gov/image-article/apod-2012-april-18-the-flight-deck-of-space-shuttle-endeavour/)

## 2. Jump aperture and flight storyboard

![Film still of a spacecraft approaching a luminous jump point](https://vignette.wikia.nocookie.net/marvel-cinematic-universe-guide/images/0/01/Jump_point.png/revision/latest?cb=20190120201928)

Use the jump-point still for the approach, when ship and aperture fit in one frame. Use the *Interstellar* renders for stars bending around the edge and the destination showing through the opening. Give our aperture a broken circular structure with three tracking pylons and a brief light sweep that reads through the windshield and across the hull. [Jump-point still](https://vignette.wikia.nocookie.net/marvel-cinematic-universe-guide/images/0/01/Jump_point.png/revision/latest?cb=20190120201928) · [Interstellar wormhole renders, figures 11–12](https://arxiv.org/pdf/1502.03809#page=10)

| Time | Story beat | Cockpit view | Chase view |
| --- | --- | --- | --- |
| **0.0–1.6 s** | The shuttle releases from the orbital dock. | Dock lights pass the side panes as the tug recedes. | The ship clears the berth with the dock and tug behind it. |
| **1.6–3.3 s** | The shuttle banks toward the jump aperture. | A service craft drifts across the view as the aperture moves into the center pane. | The bank reveals the underside and takes the radiator fins from face-on to edge-on. |
| **3.3–4.6 s** | The shuttle crosses the aperture. | The moon appears through the opening. The rim bends the star field and a soft light sweep moves across the glass. | The full ship crosses the ring in silhouette. The light sweep moves over its panels and engines. |
| **4.6–7.5 s** | The shuttle emerges, extends its gear, and lands. | Pad lights align through the center pane. The relay station grows into view. | The gear deploys in view. The pad, docking arm, and waiting rover establish scale at touchdown. |

Use a steady, layered glow with a clear horizon coming out of the jump. Stage the best geometry comparisons during the bank and the descent, when the ship and surrounding objects have legible outlines.

## 3. Moon relay station and its objects

![NASA Gateway station concept with long solar arrays and connected modules](https://images-assets.nasa.gov/image/Artemis%20-%20Gateway%20-%20Artemis%20IV%20-%20Solar%20Arrays/Artemis%20-%20Gateway%20-%20Artemis%20IV%20-%20Solar%20Arrays~large.jpg)

Use Gateway for the shape language of connected modules, trusses, and solar wings. Adapt it into a ground installation. The arrival shot should show the pad, a raised habitat, and a dish silhouetted against the sky. From the landing point, the airlock and rover should be visible together. [NASA Gateway concept gallery](https://www.nasa.gov/gallery/gateway-artists-concepts/)

| Object | Visual brief | Image reference |
| --- | --- | --- |
| **Docking arm** | Two articulated elbows, guarded joints, visible cable routing, and an open grapple head. Put a smooth pressure tank beside it so their shapes are easy to compare. | [Canadarm2 photo](https://www.nasa.gov/image-article/a-robotic-helping-hand/) · [Grapple-head close-up](https://www.nasa.gov/image-article/grappling-with-future/) |
| **Engine pods and radiators** | Build nozzle ribs, flanged pipes, and separated fins. Use silhouettes that change as the shuttle banks. | [NASA RS-25 hardware photos, page 19](https://www.nasa.gov/wp-content/uploads/2021/05/osbp_htdb_stennis_final_may19_2021.pdf#page=19) · [NASA station radiator study](https://ntrs.nasa.gov/api/citations/20260005820/downloads/ICES_MARVL_STARK_v3.pdf) |
| **Landing gear** | Give the gear a readable folded and deployed profile, a visible strut, and a scale cue at touchdown. | [Sierra Space transportation guide](https://www.sierraspace.com/wp-content/uploads/2022/09/DD3325_SPC_Company-Overview-Guide_DIGITAL.pdf) |
| **Maintenance rover** | A compact rover with exposed wheels, a sensor mast, and a small guide light. Animate it waiting beside the pad and rolling toward the airlock. | [NASA VIPER rover concept](https://www.nasa.gov/centers-and-facilities/ames/viper-completes-its-critical-design-review/) |
| **Relay dish and uplink** | Use an open dish frame with support struts and a readable connection to the station. Make the uplink console bright enough to identify from inside the airlock. | [Voyager antenna construction photo](https://science.nasa.gov/photojournal/voyager-antenna-dish-construction-2/) |
| **Distant comet** | A small uneven silhouette with a faint tail during approach. Keep enough space between it and the ring so both shapes read. | [Rosetta comet photograph](https://science.nasa.gov/image-detail/comet-67p/) |

## 4. Geometry and visual verification

- Use four measured levels for managed assemblies, **L0** coarsest through **L3** finest. Keep pivots, scale, and slot layout consistent across levels. Treat engine pods, fins, landing gear, and grapple arms as separate assemblies where an independent LOD choice makes sense.
- Model shape-defining gaps, fins, and struts. Use materials for fine scratches, painted panel seams, and decals. Compare each candidate mesh to the finest version from the actual cockpit and chase camera angles.
- Profile pilot caps of **32,000**, **64,000**, and **112,000 selected triangles** once the new geometry is in place. Confirm from both camera routes that the coarsest visible managed meshes fit each chosen cap. Use the cost measurements to settle the final display budgets.
- Capture fixed-camera comparisons during the bank and approach. Hold the ship pose, lights, effects, and camera fixed while capturing the three policies and the full-detail reference. Use full frames to orient the viewer and matching crops to show the hardware.

## Build checklist

### A. Blockout and cameras

- [x] Connect the existing dock’s extraction hatch to an identifiable shuttle berth.
- [x] Block out the shuttle, windshield, consoles, jump aperture, and moon landing pad.
- [x] Establish the 7.5-second ship path from berth release to touchdown.
- [x] Review both camera rigs at departure, bank, jump entry, exit, and touchdown.
- [x] Check that the cockpit sees the aperture and pad clearly, and that the chase camera keeps the ship clear of the station silhouettes.

**Stage A review.** After confirming extraction in Orbital Salvage, follow the boarding marker and press **E** at the shuttle. In Shuttle Flight, press **1** for cockpit and **2** for chase. **Space** pauses playback and **L** advances to the next landmark. The review captures are in `Results/orbital-study/flight-stage-a/`. The cockpit still has blockout framing and simple console surfaces for the next art pass.

### B. Shuttle and jump visuals

- [x] Model the exterior hull, pane frames, two engine pods, radiators, and folding gear.
- [x] Build the cockpit with a clear forward view, restrained instrument glow, and painted wear on frequently handled surfaces.
- [x] Animate berth release, banking, jump crossing, gear extension, and touchdown.
- [x] Add the aperture pylons, star distortion, and light sweep. Review the effect from both cameras at normal playback speed.
- [x] Check that cockpit and exterior materials match where the two views share the same hull or glass.

**Stage B review.** The cockpit and chase recordings and the paired review stills are in `Results/orbital-study/flight-stage-b/`. Both recordings cover the full 7.5 seconds. Play Mode verification covered camera switching without moving the ship, flight timing, and the final hold at touchdown.

### C. Relay station and closing shot

- [x] Dress the pad with rails, landing lights, docking arm, power cabling, and habitat modules.
- [x] Add the rover arrival beat and a clear route from the pad through the airlock.
- [x] Add the relay dish, uplink cradle, and a visible amber-to-green change when the core is seated.
- [x] Compose the final shot so the recovered core and the station’s relay action are easy to read.

**Stage C review.** Press **E** after touchdown to disembark with the core. Follow the rover through the airlock, then press **E** at the uplink. The ending shows the seated core and green status lights before the camera lifts to the turning relay dish. The four review stills are in `Results/orbital-study/relay-stage-c/`.

### D. LOD and render sign-off

- [ ] Build and verify four LODs for the managed assemblies. Record measured triangle counts and asset provenance.
- [ ] Inspect silhouettes and clipping from both flight cameras at all landmark times.
- [ ] Verify that the shuttle and station remain readable in shaded Play Mode renders and in the standalone player.
- [ ] Export an arrival still, one paired cockpit/chase still from the same flight moment, a jump entry/exit pair, and a station landing still.
- [ ] Export a fixed-pose comparison with actual triangle spend and close-ups of at least two competing assemblies.
- [ ] Record a complete 7.5-second clip in each camera and one clip with a camera switch.

**First approval milestone.** Review the blockout of the cockpit approaching the aperture, the chase view banking toward it, and the landing pad with the docking arm and rover in frame. These three shots should work before the final asset and lighting pass.

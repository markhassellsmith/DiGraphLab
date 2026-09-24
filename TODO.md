# DiGraphLab — Running To-Do List

This file is the canonical, committed running to-do list for the Harmony work. I will update and commit it as I propose or complete tasks so items do not get lost in chat history.

Format: [Status] Task — short description (owner, notes)

## High priority
- [In Progress] Add notation toggle and key selector in WPF viewer — UI controls to switch Traditional/Nashville/Roman and set tonic (Git: committed). (assistant)
- [In Progress] Inspector panel — show selected node details and provide playback hook (Git: committed). (assistant)

## Ready / Next (short-term)
- [Todo] Replace vis-local renderer with bundled vis-network or D3 for richer layout and interactions. (assistant)
- [Todo] Implement playback for inspector Play button (NAudio / MIDI out). (assistant / user to choose audio target)
- [Todo] Add unit tests for Chord inference, ChordFormatter, ChordLabeler, and VoiceLeading. (assistant)

## Medium term
- [Todo] Improve voice-leading metric (Hungarian algorithm / register-aware matching). (assistant)
- [Todo] Store multiple exemplar voicings per Nashville node and pick lowest-cost pairing when computing edges. (assistant)
- [Todo] Implement saving/loading (deserialize) HarmonyGraph from exported JSON. (assistant)

## Long term / Wishlist
- [Todo] Full enharmonic spelling & key-signature-aware labeling (accurate accidentals, double-sharps/flats). (assistant)
- [Todo] Style packs (Jazz/Pop) and voicing libraries. (assistant)
- [Todo] Functional analysis (secondary dominants, V/V detection) and Roman labeling improvements. (assistant)
- [Todo] Native WPF renderer (SkiaSharp or MSAGL) implementing same renderer API so WebView2 is optional. (assistant)
- [Todo] CI: build/test pipeline and automated demo generation. (assistant)

## Completed / Done (examples)
- [Done] Core Chord model improvements: pitch-class normalization, inversion helpers, diatonic factories. (assistant)
- [Done] ChordFormatter (traditional + Nashville) and ChordLabeler (Roman numerals) implemented. (assistant)
- [Done] HarmonyGraph, VoiceLeading, HarmonyService, GraphAdapter implemented. (assistant)
- [Done] Demo app and WPF WebView2 viewer with local renderer and inspector. (assistant)

--
I will update this file and commit changes whenever I propose or complete new tasks. If you prefer a different tracking mechanism (GitHub Issues, Project Board, or a CSV), tell me and I will add it.

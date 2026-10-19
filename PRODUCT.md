# Product

## Register

product

## Users

Four roles at an industrial process plant (refinery, chemical or utilities site):

- **Staff** (operators, field workers): spot a fault on a pump, valve or exchanger and log it quickly, often from a
  shared control-room PC or a tablet on the floor, sometimes wearing gloves. Their job: report it once, correctly,
  and move on.
- **Supervisors**: start of shift, triage the queue of new faults, approve real work, assign it to technicians,
  and keep an eye on what is ageing. Their job: nothing critical sits unapproved or unassigned.
- **Technicians**: see what is assigned to them, start it, close it with notes. Their job: a clear personal queue.
- **Admins**: create accounts and assign roles.

## Product Purpose

PlantOps tracks equipment maintenance work orders from fault report to closure (Open → Approved → Assigned →
In progress → Closed) with an audit trail of every change. Success: faults are logged in under a minute, nothing
critical waits unnoticed, every user sees only the actions they are allowed to take, and anyone can see who did
what and when.

## Brand Personality

Calm, precise, industrial. The interface behaves like a modern control-room HMI: quiet and neutral when things are
normal, so the few things that need attention (Critical and High priority, ageing work, errors) stand out
immediately. Voice is plain and specific: equipment tags, statuses and times, no marketing words.

## Anti-references

- SaaS dashboards built from hero metrics, gradient accents and identical card grids.
- Consumer-app softness: pastel everything, bubbly shapes, playful illustrations.
- Rainbow status colouring where every status has its own loud colour, so nothing stands out.
- Dense legacy enterprise screens (old SAP PM, Maximo) with dozens of equally weighted fields.

Lean toward Linear and GitHub issues: dense, scannable lists, quiet chrome, strong typography, keyboard friendly.

## Design Principles

1. **Colour means attention.** Neutral by default; saturated colour is reserved for priority, abnormal state and
   errors, so a glance at a list tells you where to look (the ISA-101 high-performance HMI idea).
2. **The queue is the product.** Lists come first: scannable rows, stable sort, filters that persist, tags and ages
   readable at a glance.
3. **Only show what you can do.** Actions appear for the roles and states that allow them; the API still enforces
   every rule, and errors say plainly why something was refused.
4. **Every change has a name and a time.** History is a first-class view, not a hidden log.
5. **Fast on a shared PC.** Minimal clicks to log a fault; keyboard and touch both work.

## Accessibility & Inclusion

WCAG 2.2 AA. Text contrast at least 4.5:1 (3:1 for large text and UI boundaries); full keyboard operation with
visible focus; status and priority never shown by colour alone (always text, plus an icon where useful);
touch targets at least 24px (44px preferred for primary actions on tablets); respects reduced-motion; works when
zoomed to 200%.

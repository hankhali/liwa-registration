import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject, input, model } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { map } from 'rxjs';
import { LanguageService } from '../core/i18n/language.service';
import { Day, TEACHING_DAYS, TimeWindow, formatTime } from '../core/planning/planning.models';

/** One class meeting on the grid. */
export interface TimetableBlock {
  key: string;
  code: string;
  label?: string;
  day: Day;
  start: string; // "10:00" or "10:00:00"
  end: string;
  /** Force the clash style (clashes between blocks are also detected automatically). */
  clash?: boolean;
}

/**
 * One grid row = 5 minutes. Class times are multiples of 5 (10:50, 12:15, 4:50 pm). With 15-minute rows a class
 * ending at 10:50 got a fractional row number, which CSS ignores, and the block was auto-placed below the grid.
 */
export const SLOT_MINUTES = 5;
export const SLOTS_PER_HOUR = 60 / SLOT_MINUTES;

export function toMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + (m || 0);
}

function hhmm(minutes: number): string {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
}

/** CSS grid row line for a time (row 1 is the day header). Always a whole number. */
export function gridRow(t: string, startHour: number, endHour: number): number {
  const minutes = Math.min(Math.max(toMinutes(t), startHour * 60), endHour * 60);
  return 2 + Math.round((minutes - startHour * 60) / SLOT_MINUTES);
}

/** End-exclusive overlap (brief §7): 10:00–10:50 and 10:50–11:40 do not clash. */
export function overlaps(a: { start: string; end: string }, b: { start: string; end: string }): boolean {
  return toMinutes(a.start) < toMinutes(b.end) && toMinutes(b.start) < toMinutes(a.end);
}

/** Hour cells → free-time windows, merging neighbouring hours per day. */
export function windowsFromHours(cells: ReadonlySet<string>, days: Day[], startHour: number, endHour: number): TimeWindow[] {
  const out: TimeWindow[] = [];
  for (const day of days) {
    let open: TimeWindow | null = null;
    for (let h = startHour; h < endHour; h++) {
      if (cells.has(`${day}.${h}`)) {
        if (open && open.end === hhmm(h * 60)) open.end = hhmm((h + 1) * 60);
        else {
          open = { day, start: hhmm(h * 60), end: hhmm((h + 1) * 60) };
          out.push(open);
        }
      } else open = null;
    }
  }
  return out;
}

/** Free-time windows → the hour cells they fully cover. */
export function hoursFromWindows(windows: TimeWindow[], startHour: number, endHour: number): Set<string> {
  const cells = new Set<string>();
  for (const w of windows)
    for (let h = startHour; h < endHour; h++)
      if (toMinutes(w.start) <= h * 60 && (h + 1) * 60 <= toMinutes(w.end)) cells.add(`${w.day}.${h}`);
  return cells;
}

/**
 * The weekly timetable (brief §9). Shows classes Monday–Friday (Saturday when a term teaches then), marks
 * clashes, lists online classes with no fixed time, shades Friday afternoon (no teaching, Jumu'ah) and the
 * student's free time. With `editable`, the same grid is the free-time editor: each hour is a toggle button.
 * On phones it becomes a per-day list (a "Tuesday" heading, then "5:00–6:15 pm · ITC201 Database Systems").
 */
@Component({
  selector: 'app-timetable-grid',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  templateUrl: './timetable-grid.component.html',
  styleUrl: './timetable-grid.component.scss',
})
export class TimetableGridComponent {
  readonly blocks = input<TimetableBlock[]>([]);
  readonly online = input<{ code: string; label?: string }[]>([]);
  readonly days = input<Day[]>(TEACHING_DAYS);
  readonly startHour = input(8);
  /** Liwa teaches until 10 pm. */
  readonly endHour = input(22);
  /** Friday teaching ends at this time; null when a term allows Friday afternoons. */
  readonly fridayCutoff = input<string | null>('12:00');
  readonly editable = input(false);
  /** Free-time windows: shaded on the grid, edited when `editable`. */
  readonly free = model<TimeWindow[]>([]);
  readonly caption = input('');

  protected readonly language = inject(LanguageService);
  private readonly narrow = toSignal(inject(BreakpointObserver).observe('(max-width: 600px)').pipe(map((r) => r.matches)), { initialValue: false });
  /** Phones never get the hour grid (UX pass): a per-day list instead. The editor stays a grid. */
  protected readonly listMode = computed(() => !this.editable() && this.narrow());
  protected readonly slotsPerHour = SLOTS_PER_HOUR;

  protected readonly hours = computed(() => Array.from({ length: this.endHour() - this.startHour() }, (_, i) => this.startHour() + i));
  protected readonly rowCount = computed(() => ((this.endHour() - this.startHour()) * 60) / SLOT_MINUTES);
  protected readonly cells = computed(() => hoursFromWindows(this.free(), this.startHour(), this.endHour()));

  /** Blocks with their grid rows, clash flags (any overlap on the same day) and side-by-side lanes. */
  protected readonly placed = computed(() => {
    const list = this.blocks().filter((b) => this.days().includes(b.day));
    return list.map((b) => {
      const sameDay = list.filter((o) => o !== b && o.day === b.day && overlaps(o, b));
      const lane = sameDay.filter((o) => list.indexOf(o) < list.indexOf(b)).length;
      return {
        ...b,
        clash: b.clash || sameDay.length > 0,
        lane,
        lanes: sameDay.length + 1,
        col: this.days().indexOf(b.day) + 2,
        rowStart: this.row(b.start),
        rowEnd: this.row(b.end),
        time: this.range(b.start, b.end),
      };
    });
  });

  protected readonly freeBlocks = computed(() =>
    this.editable()
      ? []
      : this.free()
          .filter((w) => this.days().includes(w.day))
          .map((w) => ({ col: this.days().indexOf(w.day) + 2, rowStart: this.row(w.start), rowEnd: this.row(w.end) })),
  );

  /** Rows of the shaded "no classes on Friday afternoon" area. */
  protected readonly fridayBlocked = computed(() => {
    const cutoff = this.fridayCutoff();
    if (!cutoff || !this.days().includes('Friday')) return null;
    return { col: this.days().indexOf('Friday') + 2, rowStart: this.row(cutoff), rowEnd: this.rowCount() + 2 };
  });

  /** Per-day list for phones and screen readers, in time order. */
  protected readonly byDay = computed(() =>
    this.days()
      .map((day) => ({
        day,
        items: this.placed()
          .filter((b) => b.day === day)
          .sort((a, b) => toMinutes(a.start) - toMinutes(b.start)),
      }))
      .filter((d) => d.items.length > 0),
  );

  protected readonly hasClash = computed(() => this.placed().some((b) => b.clash));

  protected row(t: string): number {
    return gridRow(t, this.startHour(), this.endHour());
  }

  protected range(start: string, end: string): string {
    const lang = this.language.lang();
    return `${formatTime(start, lang)}–${formatTime(end, lang)}`;
  }

  protected hourLabel(h: number): string {
    return formatTime(`${h}:00`, this.language.lang());
  }

  protected cellAllowed(day: Day, h: number): boolean {
    const cutoff = this.fridayCutoff();
    return !(day === 'Friday' && cutoff !== null && h * 60 >= toMinutes(cutoff));
  }

  protected isFree(day: Day, h: number): boolean {
    return this.cells().has(`${day}.${h}`);
  }

  protected toggle(day: Day, h: number): void {
    if (!this.editable() || !this.cellAllowed(day, h)) return;
    const next = new Set(this.cells());
    const key = `${day}.${h}`;
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.free.set(windowsFromHours(next, this.days(), this.startHour(), this.endHour()));
  }

  /** Select or clear a whole day in the editor. */
  protected toggleDay(day: Day): void {
    if (!this.editable()) return;
    const allowed = this.hours().filter((h) => this.cellAllowed(day, h));
    const allOn = allowed.every((h) => this.isFree(day, h));
    const next = new Set(this.cells());
    for (const h of allowed) {
      if (allOn) next.delete(`${day}.${h}`);
      else next.add(`${day}.${h}`);
    }
    this.free.set(windowsFromHours(next, this.days(), this.startHour(), this.endHour()));
  }
}

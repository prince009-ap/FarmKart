import { TranslatePipe } from '../core/pipes/translate.pipe';
import {
  Component,
  Input,
  OnInit,
  OnDestroy,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  output
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

export type AuctionTimerStatus = 'UPCOMING' | 'LIVE' | 'ENDED' | 'CANCELLED';

export interface AuctionTimerState {
  status: AuctionTimerStatus;
  totalSeconds: number;
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  label: string;
}

@Component({
  selector: 'app-auction-countdown',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, MatIconModule],
  template: `
    <ng-container [ngSwitch]="state().status">

      <!-- UPCOMING -->
      <ng-container *ngSwitchCase="'UPCOMING'">
        <div class="flex flex-col items-center gap-1.5">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold tracking-wide bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400">
            <mat-icon class="!w-3.5 !h-3.5 text-sm">schedule</mat-icon>
            UPCOMING
          </span>
          <div *ngIf="!hideCountdown" class="space-y-0.5 text-center">
            <p class="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">{{ state().label }}</p>
            <p class="font-mono font-black text-lg text-amber-700 dark:text-amber-400 tabular-nums">{{ formatTime() }}</p>
          </div>
        </div>
      </ng-container>

      <!-- LIVE -->
      <ng-container *ngSwitchCase="'LIVE'">
        <div class="flex flex-col items-center gap-1.5">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold tracking-wide bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400">
            <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            LIVE
          </span>
          <div *ngIf="!hideCountdown" class="space-y-0.5 text-center">
            <p class="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">{{ state().label }}</p>
            <p class="font-mono font-black text-lg text-emerald-700 dark:text-emerald-400 tabular-nums">{{ formatTime() }}</p>
          </div>
        </div>
      </ng-container>

      <!-- ENDED -->
      <ng-container *ngSwitchCase="'ENDED'">
        <div class="flex flex-col items-center gap-1.5">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold tracking-wide bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
            <mat-icon class="!w-3.5 !h-3.5 text-sm">check_circle</mat-icon>
            ENDED
          </span>
          <p *ngIf="!hideCountdown" class="text-[10px] font-semibold text-slate-500">Auction ended</p>
        </div>
      </ng-container>

      <!-- CANCELLED -->
      <ng-container *ngSwitchCase="'CANCELLED'">
        <div class="flex flex-col items-center gap-1.5">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold tracking-wide bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-400">
            <mat-icon class="!w-3.5 !h-3.5 text-sm">cancel</mat-icon>
            CANCELLED
          </span>
        </div>
      </ng-container>

    </ng-container>
  `
})
export class AuctionCountdownComponent implements OnInit, OnDestroy, OnChanges {
  @Input({ required: true }) startTimeUtc!: string;
  @Input({ required: true }) endTimeUtc!: string;
  @Input() serverTimeUtc?: string;
  @Input() hideCountdown = false;

  readonly statusChanged = output<AuctionTimerStatus>();

  state = signal<AuctionTimerState>({
    status: 'UPCOMING',
    totalSeconds: 0,
    days: 0,
    hours: 0,
    minutes: 0,
    seconds: 0,
    label: 'Starts in'
  });

  private intervalId?: ReturnType<typeof setInterval>;
  private serverOffsetMs = 0;
  private previousStatus?: AuctionTimerStatus;

  ngOnInit(): void {
    this.syncServerOffset();
    this.tick();
    this.intervalId = setInterval(() => this.tick(), 1000);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['serverTimeUtc'] || changes['startTimeUtc'] || changes['endTimeUtc']) {
      this.syncServerOffset();
      this.tick();
    }
  }

  ngOnDestroy(): void {
    if (this.intervalId !== undefined) {
      clearInterval(this.intervalId);
      this.intervalId = undefined;
    }
  }

  private syncServerOffset(): void {
    if (this.serverTimeUtc) {
      const serverMs = new Date(this.serverTimeUtc).getTime();
      const clientMs = Date.now();
      this.serverOffsetMs = serverMs - clientMs;
    } else {
      this.serverOffsetMs = 0;
    }
  }

  private getAdjustedNow(): Date {
    return new Date(Date.now() + this.serverOffsetMs);
  }

  private tick(): void {
    const now = this.getAdjustedNow();
    const start = new Date(this.startTimeUtc);
    const end = new Date(this.endTimeUtc);

    let status: AuctionTimerStatus;
    let targetMs: number;
    let label: string;

    if (now < start) {
      status = 'UPCOMING';
      targetMs = start.getTime() - now.getTime();
      label = 'Starts in';
    } else if (now <= end) {
      status = 'LIVE';
      targetMs = end.getTime() - now.getTime();
      label = 'Ends in';
    } else {
      status = 'ENDED';
      targetMs = 0;
      label = '';
    }

    const clampedMs = Math.max(0, targetMs);
    const totalSeconds = Math.floor(clampedMs / 1000);
    const days = Math.floor(totalSeconds / 86400);
    const hours = Math.floor((totalSeconds % 86400) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    this.state.set({ status, totalSeconds, days, hours, minutes, seconds, label });

    if (status !== this.previousStatus) {
      this.previousStatus = status;
      this.statusChanged.emit(status);
    }
  }

  formatTime(): string {
    const s = this.state();
    const hh = String(s.hours).padStart(2, '0');
    const mm = String(s.minutes).padStart(2, '0');
    const ss = String(s.seconds).padStart(2, '0');

    if (s.days > 0) {
      return `${s.days}d ${hh}:${mm}:${ss}`;
    }
    return `${hh}:${mm}:${ss}`;
  }
}

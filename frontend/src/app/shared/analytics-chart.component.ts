import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TimeSeriesChart, TimeSeriesPoint } from '../core/models/analytics.models';

@Component({
  selector: 'app-analytics-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="relative w-full overflow-hidden rounded-2xl bg-slate-900/80 border border-slate-800/80 p-5 shadow-xl backdrop-blur-md">
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h4 class="text-sm font-bold text-white tracking-wide uppercase flex items-center gap-2">
            <span class="w-2 h-2 rounded-full" [ngClass]="colorDotClass"></span>
            {{ chartData?.metricName || title || 'Analytics Chart' }}
          </h4>
          <p class="text-[11px] text-slate-400 font-medium mt-0.5">
            Grouped {{ chartData?.timeGroup || 'Daily' }} • {{ points.length }} data points
          </p>
        </div>
        <div class="text-right" *ngIf="totalValue > 0">
          <span class="text-xs text-slate-400 font-semibold uppercase block">Total</span>
          <span class="text-base font-extrabold text-emerald-400">
            {{ isCurrency ? '₹' + (totalValue | number:'1.0-2') : (totalValue | number:'1.0-2') }}
          </span>
        </div>
      </div>

      <!-- Chart Canvas SVG -->
      <div class="relative w-full" [style.height.px]="height">
        <!-- Empty State -->
        <div *ngIf="!points || points.length === 0" class="absolute inset-0 flex flex-col items-center justify-center text-slate-500 bg-slate-950/40 rounded-xl border border-dashed border-slate-800">
          <svg class="w-10 h-10 mb-2 stroke-current opacity-40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
          </svg>
          <span class="text-xs font-semibold">No analytics data recorded for this time range</span>
        </div>

        <!-- SVG Rendering -->
        <svg *ngIf="points && points.length > 0" class="w-full h-full overflow-visible" [attr.viewBox]="'0 0 ' + viewBoxWidth + ' ' + viewBoxHeight" preserveAspectRatio="none">
          <!-- Background Grid Ticks -->
          <g class="opacity-20 stroke-slate-700" stroke-dasharray="3 3">
            <line x1="45" y1="20" [attr.x2]="viewBoxWidth - 10" y2="20" />
            <line x1="45" [attr.y1]="(viewBoxHeight - 40)/2 + 20" [attr.x2]="viewBoxWidth - 10" [attr.y2]="(viewBoxHeight - 40)/2 + 20" />
            <line x1="45" [attr.y1]="viewBoxHeight - 30" [attr.x2]="viewBoxWidth - 10" [attr.y2]="viewBoxHeight - 30" />
          </g>

          <!-- Y-Axis Labels -->
          <text x="40" y="24" text-anchor="end" class="fill-slate-400 text-[10px] font-bold">{{ maxValueFormatted }}</text>
          <text x="40" [attr.y]="(viewBoxHeight - 40)/2 + 24" text-anchor="end" class="fill-slate-400 text-[10px] font-bold">{{ midValueFormatted }}</text>
          <text x="40" [attr.y]="viewBoxHeight - 26" text-anchor="end" class="fill-slate-400 text-[10px] font-bold">0</text>

          <!-- BAR CHART -->
          <g *ngIf="chartType === 'bar'">
            <g *ngFor="let p of computedPoints; let i = index">
              <!-- Bar Column -->
              <rect
                [attr.x]="p.x - barWidth/2"
                [attr.y]="p.y"
                [attr.width]="barWidth"
                [attr.height]="p.height"
                rx="4"
                [attr.fill]="barFillColor"
                class="transition-all duration-300 hover:brightness-125 cursor-pointer opacity-90 hover:opacity-100"
              >
                <title>{{ p.label }}: {{ isCurrency ? '₹' + (p.value | number:'1.0-2') : p.value }}</title>
              </rect>
              <!-- X Label (Show selectively if many points) -->
              <text
                *ngIf="shouldShowLabel(i)"
                [attr.x]="p.x"
                [attr.y]="viewBoxHeight - 10"
                text-anchor="middle"
                class="fill-slate-400 text-[9px] font-semibold"
              >
                {{ p.label }}
              </text>
            </g>
          </g>

          <!-- LINE CHART -->
          <g *ngIf="chartType === 'line'">
            <!-- Area Fill -->
            <path *ngIf="points.length > 1" [attr.d]="areaPath" [attr.fill]="areaFillColor" class="opacity-20" />
            <!-- Line Path -->
            <path *ngIf="points.length > 1" [attr.d]="linePath" fill="none" [attr.stroke]="lineStrokeColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
            
            <!-- Pillar Bar fallback if only 1 point -->
            <rect
              *ngIf="points.length === 1 && computedPoints.length === 1"
              [attr.x]="computedPoints[0].x - 12"
              [attr.y]="computedPoints[0].y"
              width="24"
              [attr.height]="computedPoints[0].height"
              rx="4"
              [attr.fill]="barFillColor"
              class="opacity-60"
            />

            <!-- Point Dots & Tooltips -->
            <g *ngFor="let p of computedPoints; let i = index">
              <circle
                [attr.cx]="p.x"
                [attr.cy]="p.y"
                r="4"
                [attr.fill]="lineStrokeColor"
                class="stroke-slate-900 stroke-2 hover:r-6 transition-all duration-200 cursor-pointer"
              >
                <title>{{ p.label }}: {{ isCurrency ? '₹' + (p.value | number:'1.0-2') : p.value }}</title>
              </circle>

              <!-- Value text on points with non-zero value -->
              <text
                *ngIf="p.value > 0"
                [attr.x]="p.x"
                [attr.y]="p.y - 8"
                text-anchor="middle"
                class="fill-emerald-400 text-[9px] font-black"
              >
                {{ isCurrency ? '₹' + (p.value | number:'1.0-0') : (p.value | number:'1.0-0') }}
              </text>

              <!-- X Label -->
              <text
                *ngIf="shouldShowLabel(i)"
                [attr.x]="p.x"
                [attr.y]="viewBoxHeight - 10"
                text-anchor="middle"
                class="fill-slate-400 text-[9px] font-semibold"
              >
                {{ p.label }}
              </text>
            </g>
          </g>
        </svg>
      </div>
    </div>
  `
})
export class AnalyticsChartComponent implements OnChanges {
  @Input() title: string = '';
  @Input() chartData: TimeSeriesChart | null = null;
  @Input() chartType: 'bar' | 'line' = 'bar';
  @Input() height: number = 220;
  @Input() isCurrency: boolean = false;
  @Input() themeColor: 'emerald' | 'amber' | 'sky' | 'indigo' | 'purple' = 'emerald';

  viewBoxWidth = 600;
  viewBoxHeight = 180;

  points: TimeSeriesPoint[] = [];
  computedPoints: { x: number; y: number; height: number; value: number; label: string }[] = [];
  totalValue = 0;
  maxValue = 0;
  barWidth = 20;
  linePath = '';
  areaPath = '';

  get colorDotClass(): string {
    switch (this.themeColor) {
      case 'amber': return 'bg-amber-400 shadow-amber-500/50 shadow-md';
      case 'sky': return 'bg-sky-400 shadow-sky-500/50 shadow-md';
      case 'indigo': return 'bg-indigo-400 shadow-indigo-500/50 shadow-md';
      case 'purple': return 'bg-purple-400 shadow-purple-500/50 shadow-md';
      default: return 'bg-emerald-400 shadow-emerald-500/50 shadow-md';
    }
  }

  get barFillColor(): string {
    switch (this.themeColor) {
      case 'amber': return '#f59e0b';
      case 'sky': return '#38bdf8';
      case 'indigo': return '#818cf8';
      case 'purple': return '#c084fc';
      default: return '#10b981';
    }
  }

  get lineStrokeColor(): string {
    return this.barFillColor;
  }

  get areaFillColor(): string {
    return this.barFillColor;
  }

  get maxValueFormatted(): string {
    if (this.maxValue === 0) return '0';
    return this.isCurrency ? '₹' + Math.round(this.maxValue) : Math.round(this.maxValue).toString();
  }

  get midValueFormatted(): string {
    if (this.maxValue === 0) return '0';
    return this.isCurrency ? '₹' + Math.round(this.maxValue / 2) : Math.round(this.maxValue / 2).toString();
  }

  shouldShowLabel(index: number): boolean {
    const total = this.computedPoints.length;
    if (total <= 12) return true;
    if (total <= 20) return index % 2 === 0 || index === total - 1;
    return index % 3 === 0 || index === total - 1;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['chartData'] || changes['chartType']) {
      this.processData();
    }
  }

  private processData(): void {
    this.points = this.chartData?.points || [];
    this.totalValue = this.points.reduce((sum, p) => sum + p.value, 0);

    if (this.points.length === 0) {
      this.computedPoints = [];
      this.linePath = '';
      this.areaPath = '';
      this.maxValue = 0;
      return;
    }

    this.maxValue = Math.max(...this.points.map(p => p.value), 1);

    const paddingLeft = 55;
    const paddingRight = 20;
    const paddingTop = 25;
    const paddingBottom = 30;

    const chartW = this.viewBoxWidth - paddingLeft - paddingRight;
    const chartH = this.viewBoxHeight - paddingTop - paddingBottom;

    const count = this.points.length;
    const stepX = count > 1 ? chartW / (count - 1) : chartW / 2;
    this.barWidth = Math.max(8, Math.min(32, (chartW / count) * 0.65));

    this.computedPoints = this.points.map((p, idx) => {
      const x = count === 1 ? paddingLeft + chartW / 2 : paddingLeft + idx * stepX;
      const ratio = p.value / this.maxValue;
      const barH = Math.max(2, ratio * chartH);
      const y = paddingTop + (chartH - barH);

      return {
        x,
        y,
        height: barH,
        value: p.value,
        label: p.label
      };
    });

    if (this.chartType === 'line' && this.computedPoints.length > 1) {
      this.linePath = this.computedPoints.reduce((acc, p, idx) => {
        return idx === 0 ? `M ${p.x} ${p.y}` : `${acc} L ${p.x} ${p.y}`;
      }, '');

      const first = this.computedPoints[0];
      const last = this.computedPoints[this.computedPoints.length - 1];
      const bottomY = paddingTop + chartH;

      this.areaPath = `${this.linePath} L ${last.x} ${bottomY} L ${first.x} ${bottomY} Z`;
    }
  }
}

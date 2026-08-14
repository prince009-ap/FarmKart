import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuctionCountdownComponent } from './auction-countdown.component';

describe('AuctionCountdownComponent', () => {
  let fixture: ComponentFixture<AuctionCountdownComponent>;
  let component: AuctionCountdownComponent;

  function mkIso(offsetMs: number): string {
    return new Date(Date.now() + offsetMs).toISOString();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuctionCountdownComponent]
    }).compileComponents();
    fixture = TestBed.createComponent(AuctionCountdownComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('displays UPCOMING status when start is in the future', () => {
    component.startTimeUtc = mkIso(3_600_000); // 1 hour from now
    component.endTimeUtc   = mkIso(7_200_000); // 2 hours from now
    fixture.detectChanges();
    expect(component.state().status).toBe('UPCOMING');
  });

  it('displays LIVE status when within auction window', () => {
    component.startTimeUtc = mkIso(-60_000);   // started 1 min ago
    component.endTimeUtc   = mkIso(3_600_000); // 1 hour remaining
    fixture.detectChanges();
    expect(component.state().status).toBe('LIVE');
  });

  it('displays ENDED status when auction is over', () => {
    component.startTimeUtc = mkIso(-7_200_000);  // 2 hrs ago
    component.endTimeUtc   = mkIso(-3_600_000);  // ended 1 hr ago
    fixture.detectChanges();
    expect(component.state().status).toBe('ENDED');
  });

  it('totalSeconds is never negative', () => {
    component.startTimeUtc = mkIso(-7_200_000);
    component.endTimeUtc   = mkIso(-3_600_000);
    fixture.detectChanges();
    expect(component.state().totalSeconds).toBeGreaterThanOrEqual(0);
  });

  it('formatTime returns HH:MM:SS for sub-day countdown', () => {
    component.startTimeUtc = mkIso(-60_000);
    component.endTimeUtc   = mkIso(3_600_000);
    fixture.detectChanges();
    const t = component.formatTime();
    expect(t).toMatch(/^\d{2}:\d{2}:\d{2}$/);
  });

  it('formatTime includes days prefix when days > 0', () => {
    component.startTimeUtc = mkIso(-1_000);
    component.endTimeUtc   = mkIso(2 * 86_400_000); // 2 days remaining
    fixture.detectChanges();
    const t = component.formatTime();
    expect(t).toContain('d ');
  });

  it('applies server time offset when serverTimeUtc is provided', () => {
    const serverAhead = new Date(Date.now() + 60_000).toISOString(); // server is 1 min ahead
    component.startTimeUtc = mkIso(3_600_000);
    component.endTimeUtc   = mkIso(7_200_000);
    component.serverTimeUtc = serverAhead;
    fixture.detectChanges();
    // With +60s offset, "starts in" should be ~1min less than without offset
    expect(component.state().totalSeconds).toBeLessThan(3600);
  });

  it('cleans up interval on destroy without error', () => {
    component.startTimeUtc = mkIso(3_600_000);
    component.endTimeUtc   = mkIso(7_200_000);
    fixture.detectChanges();
    expect(() => fixture.destroy()).not.toThrow();
  });

  it('emits statusChanged when status transitions', () => {
    const events: string[] = [];
    component.statusChanged.subscribe((s: string) => events.push(s));
    component.startTimeUtc = mkIso(3_600_000);
    component.endTimeUtc   = mkIso(7_200_000);
    fixture.detectChanges();
    expect(events).toContain('UPCOMING');
  });

  it('shows correct label for upcoming auction', () => {
    component.startTimeUtc = mkIso(3_600_000);
    component.endTimeUtc   = mkIso(7_200_000);
    fixture.detectChanges();
    expect(component.state().label).toBe('Starts in');
  });

  it('shows correct label for live auction', () => {
    component.startTimeUtc = mkIso(-1_000);
    component.endTimeUtc   = mkIso(3_600_000);
    fixture.detectChanges();
    expect(component.state().label).toBe('Ends in');
  });

  it('hideCountdown does not affect state computation', () => {
    component.startTimeUtc = mkIso(-1_000);
    component.endTimeUtc   = mkIso(3_600_000);
    component.hideCountdown = true;
    fixture.detectChanges();
    expect(component.state().status).toBe('LIVE');
  });
});

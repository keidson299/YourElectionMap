import { useId } from 'react'
import { DataCredit } from '../DataCredit'
import { LineSwatch } from './LineSwatch'
import {
  BallotIcon,
  CalendarIcon,
  CheckIcon,
  ChevronIcon,
  PeopleIcon,
} from '../icons'
import { countOf, districtName, formatElectionDate } from '../../districts/labels'
import { layerRole, type MapLayers } from '../../districts/selection'
import type { District, ElectionSummary } from '../../districts/types'
import './DistrictRail.css'

// A radio group: one Tab stop, arrow keys to move, and "3 of 6" announced with
// no keyboard code of our own. On a phone it becomes a sheet under the map.

type DistrictRailProps = {
  districts: readonly District[]
  layers: MapLayers
  election: ElectionSummary | null
  /** The phone layout: a sheet with a handle, a hint, and a one-line footer. */
  compact: boolean
  sheetExpanded: boolean
  onToggleSheet: () => void
  onSelect: (districtId: string) => void
}

export function DistrictRail({
  districts,
  layers,
  election,
  compact,
  sheetExpanded,
  onToggleSheet,
  onSelect,
}: DistrictRailProps) {
  const baseId = useId()
  const titleId = `${baseId}-title`
  const listId = `${baseId}-list`
  const sourceTag = <span className="source-tag">U.S. Census Bureau TIGER</span>

  return (
    <section className="district-rail" aria-labelledby={titleId}>
      {compact ? (
        <button
          type="button"
          className="district-rail__handle"
          aria-expanded={sheetExpanded}
          aria-controls={listId}
          onClick={onToggleSheet}
        >
          <span className="visually-hidden">District list</span>
        </button>
      ) : null}

      <div className="district-rail__head">
        {compact ? null : (
          <p className="district-rail__eyebrow">
            <span>Your maps</span>
            {sourceTag}
          </p>
        )}
        <div className="district-rail__title-row">
          <h2 id={titleId} className="district-rail__title">
            {countOf(districts.length, 'district covers', 'districts cover')} you
          </h2>
          {compact ? (
            <p className="district-rail__hint">
              {sourceTag}
              <span>Tap to draw</span>
            </p>
          ) : null}
        </div>
        {compact ? null : (
          <p className="district-rail__lede">
            Select one to draw its boundary on the map.
          </p>
        )}
      </div>

      <div
        id={listId}
        className="district-rail__list"
        role="radiogroup"
        aria-labelledby={titleId}
      >
        {districts.map((district) => {
          const role = layerRole(layers, district)
          const selected = role === 'selected'
          const rowId = `${baseId}-${district.id}`

          return (
            <label
              key={district.id}
              className={selected ? 'district-row district-row--selected' : 'district-row'}
            >
              <input
                type="radio"
                className="visually-hidden"
                name={baseId}
                value={district.id}
                checked={selected}
                onChange={() => onSelect(district.id)}
                aria-labelledby={`${rowId}-name`}
                aria-describedby={`${rowId}-detail`}
              />
              <span className="district-row__body">
                <LineSwatch
                  role={role}
                  tone={selected ? 'accent' : 'muted'}
                  width={compact ? 22 : 26}
                />
                <span className="district-row__text">
                  <span id={`${rowId}-name`} className="district-row__name">
                    {districtName(district)}
                  </span>
                  <span id={`${rowId}-detail`} className="district-row__detail">
                    {selected ? 'Shown on the map' : district.detail}
                  </span>
                </span>
                {selected ? (
                  <CheckIcon className="district-row__icon" />
                ) : (
                  <ChevronIcon className="district-row__icon" />
                )}
              </span>
            </label>
          )
        })}
      </div>

      <DataCredit className="district-rail__credit" />

      {election === null ? null : compact ? (
        <ElectionStrip election={election} />
      ) : (
        <ElectionFooter election={election} />
      )}
    </section>
  )
}

function ElectionFooter({ election }: { election: ElectionSummary }) {
  return (
    <div className="district-rail__foot">
      <p className="district-rail__foot-title">Also for this address</p>
      <ul className="district-rail__facts">
        <li className="district-rail__fact">
          <CalendarIcon />
          <span>
            Next election · <strong>{formatElectionDate(election.date)}</strong>
          </span>
        </li>
        {election.candidates === null ? null : (
          <li className="district-rail__fact">
            <PeopleIcon />
            <span>
              <strong>
                {countOf(election.candidates, 'candidate', 'candidates')}
              </strong>{' '}
              on your ballot
            </span>
          </li>
        )}
        {election.measures === null ? null : (
          <li className="district-rail__fact">
            <BallotIcon />
            <strong>
              {countOf(election.measures, 'ballot measure', 'ballot measures')}
            </strong>
          </li>
        )}
      </ul>
    </div>
  )
}

function ElectionStrip({ election }: { election: ElectionSummary }) {
  return (
    <p className="district-rail__strip">
      <CalendarIcon />
      <span>
        Next election <strong>{formatElectionDate(election.date)}</strong>
        {election.candidates === null
          ? null
          : ` · ${countOf(election.candidates, 'candidate', 'candidates')}`}
      </span>
    </p>
  )
}
